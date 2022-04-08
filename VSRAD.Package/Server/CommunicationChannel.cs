using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Server
{
    public interface ICommunicationChannel
    {
        event EventHandler ConnectionStateChanged;

        ServerConnectionOptions ConnectionOptions { get; }

        ClientState ConnectionState { get; }

        Task<T> SendWithReplyAsync<T>(ICommand command, CancellationToken cancellationToken) where T : IResponse;

        Task<IReadOnlyDictionary<string, string>> GetRemoteEnvironmentAsync();

        Task<DebugServer.IPC.CapabilityInfo> GetServerCapabilityInfoAsync(CancellationToken cancellationToken);

        void ForceDisconnect();
    }

    public sealed class ConnectionRefusedException : UserException
    {
        public ConnectionRefusedException(ServerConnectionOptions connection) :
            base($"Unable to establish connection to the debug server at host {connection}")
        { }
    }

    public sealed class UnsupportedServerVersionException : UserException
    {
        public UnsupportedServerVersionException(ServerConnectionOptions connection) :
            base($"The debug server on host {connection} is out of date and missing critical features. Please update it to the latest available version.")
        { }
    }

    public enum ClientState
    {
        Disconnected,
        Connecting,
        Connected
    }

    [Export(typeof(ICommunicationChannel))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class CommunicationChannel : ICommunicationChannel
    {
        public event EventHandler ConnectionStateChanged;
        public ServerConnectionOptions ConnectionOptions =>
            _project.Options?.Connection ?? new ServerConnectionOptions("Remote address is not specified", 0);

        private ClientState _state = ClientState.Disconnected;
        public ClientState ConnectionState
        {
            get => _state;
            set
            {
                _state = value;
                ConnectionStateChanged?.Invoke(this, null);
            }
        }

        public DebugServer.IPC.CapabilityInfo ServerCapabilities { get; private set; }

        private static readonly TimeSpan _connectionTimeout = new TimeSpan(hours: 0, minutes: 0, seconds: 5);

        private readonly OutputWindowWriter _outputWindowWriter;
        private readonly IProject _project;

        private TcpClient _connection;
        private IReadOnlyDictionary<string, string> _remoteEnvironment;

        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1);

        [ImportingConstructor]
        public CommunicationChannel(SVsServiceProvider provider, IProject project)
        {
            _outputWindowWriter = new OutputWindowWriter(provider,
                Constants.OutputPaneServerGuid, Constants.OutputPaneServerTitle);
            _project = project;
            _project.RunWhenLoaded((options) =>
                options.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(options.ActiveProfile)) ForceDisconnect(); });
        }

        public Task<T> SendWithReplyAsync<T>(ICommand command, CancellationToken cancellationToken) where T : IResponse =>
            SendWithReplyAsync<T>(command, tryReconnect: true, cancellationToken);

        private async Task<T> SendWithReplyAsync<T>(ICommand command, bool tryReconnect, CancellationToken cancellationToken) where T : IResponse
        {
            await _mutex.WaitAsync();
            try
            {
                await EstablishServerConnectionAsync(cancellationToken).ConfigureAwait(false);
                using (cancellationToken.Register(ForceDisconnect))
                {
                    var bytesSent = await _connection.GetStream().WriteSerializedMessageAsync(command).ConfigureAwait(false);
                    await _outputWindowWriter.PrintMessageAsync($"Sent command ({bytesSent} bytes) to {ConnectionOptions}", command.ToString()).ConfigureAwait(false);

                    var (response, bytesReceived) = await _connection.GetStream().ReadSerializedResponseAsync<T>().ConfigureAwait(false);
                    await _outputWindowWriter.PrintMessageAsync($"Received response ({bytesReceived} bytes) from {ConnectionOptions}", response.ToString()).ConfigureAwait(false);
                    return response;
                }
            }
            catch (ObjectDisposedException) // ForceDisconnect has been called within the try block 
            {
                throw new OperationCanceledException();
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested && !(e is UnsupportedServerVersionException)) // Don't attempt to reconnect to an unsupported server
            {
                ForceDisconnect(); // At this point, the stream may be corrupted while we are still connected (e.g. in case of EndOfStreamException), so close the connection first
                if (tryReconnect)
                {
                    await _outputWindowWriter.PrintMessageAsync($"Connection to {ConnectionOptions} lost, attempting to reconnect...").ConfigureAwait(false);
                    _mutex.Release();
                    return await SendWithReplyAsync<T>(command, false, cancellationToken);
                }
                else
                {
                    await _outputWindowWriter.PrintMessageAsync($"Could not reconnect to {ConnectionOptions}").ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                _mutex.Release();
            }
        }

        public async Task<IReadOnlyDictionary<string, string>> GetRemoteEnvironmentAsync()
        {
            if (_remoteEnvironment == null)
            {
                var environment = await SendWithReplyAsync<EnvironmentVariablesListed>(new ListEnvironmentVariables(), CancellationToken.None);
                _remoteEnvironment = environment.Variables;
            }
            return _remoteEnvironment;
        }

        public async Task<DebugServer.IPC.CapabilityInfo> GetServerCapabilityInfoAsync(CancellationToken cancellationToken)
        {
            if (ConnectionState != ClientState.Connected)
                await EstablishServerConnectionAsync(cancellationToken);
            return ServerCapabilities;
        }

        public void ForceDisconnect()
        {
            _connection?.Close();
            _connection = null;
            _remoteEnvironment = null;
            ConnectionState = ClientState.Disconnected;
        }

        private async Task EstablishServerConnectionAsync(CancellationToken cancellationToken)
        {
            if (_connection != null && _connection.Connected) return;

            ConnectionState = ClientState.Connecting;

            var client = new TcpClient();
            try
            {
                using (var timeoutCts = new CancellationTokenSource(_connectionTimeout))
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
                using (cts.Token.Register(() => client.Close()))
                {
                    await client.ConnectAsync(ConnectionOptions.RemoteMachine, ConnectionOptions.Port).ConfigureAwait(false);

                    var capCommand = new GetServerCapabilitiesCommand { ExtensionCapabilities = DebugServer.IPC.CapabilityInfo.LatestExtensionCapabilities };
                    await client.GetStream().WriteSerializedMessageAsync(capCommand).ConfigureAwait(false);
                    try
                    {
                        var (response, _) = await client.GetStream().ReadSerializedResponseAsync<GetServerCapabilitiesResponse>().ConfigureAwait(false);
                        ServerCapabilities = response.Info;
                    }
                    catch (EndOfStreamException)
                    {
                        ConnectionState = ClientState.Disconnected;
                        throw new UnsupportedServerVersionException(ConnectionOptions);
                    }
                }

                if (!ServerCapabilities.IsUpToDate())
                    Errors.ShowWarning($"The debug server on host {ConnectionOptions} is out of date. Some features may not work properly.");

                _connection = client;
                ConnectionState = ClientState.Connected;
            }
            catch (Exception e) when (!(e is UnsupportedServerVersionException))
            {
                ConnectionState = ClientState.Disconnected;
                throw new ConnectionRefusedException(ConnectionOptions);
            }
        }
    }
}
