using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
        event Action ConnectionStateChanged;

        ServerConnectionOptions ConnectionOptions { get; }

        ClientState ConnectionState { get; }

        DebugServer.IPC.CapabilityInfo ServerCapabilities { get; }

        Task<T> SendWithReplyAsync<T>(ICommand command) where T : IResponse;

        Task<IReadOnlyDictionary<string, string>> GetRemoteEnvironmentAsync();

        void ForceDisconnect();
    }

    public sealed class ConnectionRefusedException : System.IO.IOException
    {
        public ConnectionRefusedException(ServerConnectionOptions connection) :
            base($"Unable to establish connection to a debug server at {connection}")
        { }
    }

    public sealed class UnsupportedServerVersionException : System.IO.IOException
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
        public event Action ConnectionStateChanged;
        public ServerConnectionOptions ConnectionOptions =>
            _project.Options?.Connection ?? new ServerConnectionOptions("Remote address is not specified", 0);

        private ClientState _state = ClientState.Disconnected;
        public ClientState ConnectionState
        {
            get => _state;
            set
            {
                _state = value;
                ConnectionStateChanged?.Invoke();
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

        public Task<T> SendWithReplyAsync<T>(ICommand command) where T : IResponse =>
            SendWithReplyAsync<T>(command, tryReconnect: true);

        private async Task<T> SendWithReplyAsync<T>(ICommand command, bool tryReconnect) where T : IResponse
        {
            await _mutex.WaitAsync();
            try
            {
                await EstablishServerConnectionAsync().ConfigureAwait(false);

                var bytesSent = await _connection.GetStream().WriteSerializedMessageAsync(command).ConfigureAwait(false);
                await _outputWindowWriter.PrintMessageAsync($"Sent command ({bytesSent} bytes) to {ConnectionOptions}", command.ToString()).ConfigureAwait(false);

                var (response, bytesReceived) = await _connection.GetStream().ReadSerializedResponseAsync<T>().ConfigureAwait(false);
                await _outputWindowWriter.PrintMessageAsync($"Received response ({bytesReceived} bytes) from {ConnectionOptions}", response.ToString()).ConfigureAwait(false);
                return response;
            }
            catch (ObjectDisposedException) // ForceDisconnect has been called within the try block 
            {
                throw new OperationCanceledException();
            }
            catch (Exception e) when (!(e is UnsupportedServerVersionException)) // Don't attempt to reconnect to an unsupported server
            {
                if (tryReconnect)
                {
                    await _outputWindowWriter.PrintMessageAsync($"Connection to {ConnectionOptions} lost, attempting to reconnect...").ConfigureAwait(false);
                    _mutex.Release();
                    return await SendWithReplyAsync<T>(command, false);
                }
                else
                {
                    ForceDisconnect();
                    await _outputWindowWriter.PrintMessageAsync($"Could not reconnect to {ConnectionOptions}").ConfigureAwait(false);
                    throw new Exception($"Connection to {ConnectionOptions} has been terminated: {e.Message}");
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
                var environment = await SendWithReplyAsync<EnvironmentVariablesListed>(new ListEnvironmentVariables());
                _remoteEnvironment = environment.Variables;
            }
            return _remoteEnvironment;
        }

        public void ForceDisconnect()
        {
            _connection?.Close();
            _connection = null;
            _remoteEnvironment = null;
            ConnectionState = ClientState.Disconnected;
        }

        private async Task EstablishServerConnectionAsync()
        {
            if (_connection != null && _connection.Connected) return;

            ConnectionState = ClientState.Connecting;

            var client = new TcpClient();
            try
            {
                using (var cts = new CancellationTokenSource(_connectionTimeout))
                using (cts.Token.Register(() => client.Dispose()))
                {
                    await client.ConnectAsync(ConnectionOptions.RemoteMachine, ConnectionOptions.Port);
                }
                await client.GetStream().WriteSerializedMessageAsync(new GetServerCapabilitiesCommand()).ConfigureAwait(false);
                var (response, _) = await client.GetStream().ReadSerializedResponseAsync<GetServerCapabilitiesResponse>().ConfigureAwait(false);
                if (response == null)
                {
                    ConnectionState = ClientState.Disconnected;
                    throw new UnsupportedServerVersionException(ConnectionOptions);
                }

                ServerCapabilities = response.Info;
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
