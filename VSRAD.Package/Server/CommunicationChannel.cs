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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VSRAD.DebugServer.IPC.Commands;

namespace VSRAD.Package.Server
{
    public interface ICommunicationChannel
    {
        event Action ConnectionStateChanged;

        ServerConnectionOptions ConnectionOptions { get; }

        ClientState ConnectionState { get; }

        Task<T> SendWithReplyAsync<T>(ICommand command) where T : IResponse;

        Task<IReadOnlyDictionary<string, string>> GetRemoteEnvironmentAsync();

        void ForceDisconnect();
    }

    public sealed class ConnectionRefusedException : IOException
    {
        public ConnectionRefusedException(ServerConnectionOptions connection) :
            base($"Unable to establish connection to a debug server at {connection}")
        { }
    }

    public sealed class UnsupportedServerVersionException : IOException
    {
        public UnsupportedServerVersionException(ServerConnectionOptions connection, Version serverVersion) :
            base($"The debug server on host {connection} is out of date and missing critical features. Please update it to the {serverVersion} or above version.")
        { }
    }

    public sealed class UnsupportedDebuggerVersionException : IOException
    {
        public UnsupportedDebuggerVersionException(ServerConnectionOptions connection, Version serverVersion) :
            base($"This extension is out of date and missing critical features to work with debug server on host {connection}. Please update extension to the {serverVersion} or above version.")
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
        private Version _extensionVersion;
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

        private static readonly TimeSpan _connectionTimeout = new TimeSpan(hours: 0, minutes: 0, seconds: 5);
        private static readonly Regex _extensionVersionRegex = new Regex(@".*\/(?<version>.*)\/RadeonAsmDebugger\.dll", RegexOptions.Compiled);

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
            _extensionVersion = typeof(CommunicationChannel).Assembly.GetName().Version;
        }

        public Task<T> SendWithReplyAsync<T>(ICommand command) where T : IResponse =>
            SendWithReplyAsync<T>(command, tryReconnect: true);

        private async Task TryProcessClientHandshakeAsync(TcpClient client)
        {
            var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
            var reader = new StreamReader(client.GetStream(), Encoding.UTF8);

            // Send client version to server
            //
            await writer.WriteLineAsync(_extensionVersion.ToString()).ConfigureAwait(false);
            // Obtain server version
            //
            var serverResponse = await reader.ReadLineAsync().ConfigureAwait(false);
            Version serverVersion = null;
            if (!Version.TryParse(serverResponse, out serverVersion))
            {
                // Inform server that client declines serve's version
                //
                await writer.WriteLineAsync(HandShakeStatus.ClientNotAccepted.ToString()).ConfigureAwait(false);
                throw new UnsupportedServerVersionException(ConnectionOptions, Constants.MinimalRequiredServerVersion);
            }

            if (serverVersion.CompareTo(Constants.MinimalRequiredServerVersion) < 0)
            {
                // Inform client that server declines client's version
                //
                await writer.WriteLineAsync(HandShakeStatus.ClientNotAccepted.ToString()).ConfigureAwait(false);
                throw new UnsupportedServerVersionException(ConnectionOptions, Constants.MinimalRequiredServerVersion);
            }

            // Inform client that server accepts client's version
            //
            await writer.WriteLineAsync(HandShakeStatus.ClientAccepted.ToString()).ConfigureAwait(false);

            // Check if client accepts server version
            //
            if (await reader.ReadLineAsync().ConfigureAwait(false) != HandShakeStatus.ServerAccepted.ToString())
            {
                throw new UnsupportedDebuggerVersionException(ConnectionOptions, serverVersion);
            }
            return;
        }

        private async Task<T> SendWithReplyAsync<T>(ICommand command, bool tryReconnect) where T : IResponse
        {
            await _mutex.WaitAsync();
            try
            {
                await EstablishServerConnectionAsync().ConfigureAwait(false);
                await _connection.GetStream().WriteSerializedMessageAsync(command).ConfigureAwait(false);
                await _outputWindowWriter.PrintMessageAsync($"Sent command to {ConnectionOptions}", command.ToString()).ConfigureAwait(false);

                var response = await _connection.GetStream().ReadSerializedMessageAsync<IResponse>().ConfigureAwait(false);
                await _outputWindowWriter.PrintMessageAsync($"Received response from {ConnectionOptions}", response.ToString()).ConfigureAwait(false);
                return (T)response;
            }
            catch (ObjectDisposedException) // ForceDisconnect has been called within the try block 
            {
                throw new OperationCanceledException();
            }
            catch (Exception e)
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
                await EstablishServerConnectionAsync().ConfigureAwait(false);
                var environment = await SendWithReplyAsync<EnvironmentVariablesListed>(new ListEnvironmentVariables()).ConfigureAwait(false);
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
#pragma warning disable CA2000 // we save this client as a _connection later if handshake processed successfully
            var client = new TcpClient();
#pragma warning restore CA2000 // Dispose objects before losing scope
            try
            {
                using (var cts = new CancellationTokenSource(_connectionTimeout))
                using (cts.Token.Register(() => client.Dispose()))
                {
                    await client.ConnectAsync(ConnectionOptions.RemoteMachine, ConnectionOptions.Port).ConfigureAwait(false);
                    await TryProcessClientHandshakeAsync(client).ConfigureAwait(false);
                    _connection = client;
                    ConnectionState = ClientState.Connected;
                }
            }
            catch (Exception e) when (!(e is UnsupportedDebuggerVersionException) && !(e is UnsupportedServerVersionException))
            {
                client.Dispose();
                ConnectionState = ClientState.Disconnected;
                throw new ConnectionRefusedException(ConnectionOptions);
            }
        }
    }
}
