using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net.Sockets;
using System.Text.RegularExpressions;
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
        public UnsupportedServerVersionException(ServerConnectionOptions connection, string minimalVersion) :
            base($"The debug server on host {connection} is out of date and missing critical features. Please update it to the {minimalVersion} or above version.")
        { }
    }

    public sealed class UnsupportedDebuggerVersionException : System.IO.IOException
    {
        public UnsupportedDebuggerVersionException(ServerConnectionOptions connection, string minimalVersion) :
            base($"This extension is out of date and missing critical features to work with debug server on host {connection}. Please update it to the {minimalVersion} or above version.")
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
            _project.Options.Profile?.General?.Connection ?? new ServerConnectionOptions("Remote address is not specified", 0);

        private string _extensionVersion;
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
            var match = _extensionVersionRegex.Match(typeof(IProject).Assembly.CodeBase);
            if (!match.Success)
                throw new Exception("Error while getting current extension version.");
            _extensionVersion = match.Groups["version"].Value;
        }

        public Task<T> SendWithReplyAsync<T>(ICommand command) where T : IResponse =>
            SendWithReplyAsync<T>(command, tryReconnect: true);

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

                await client.GetStream().WriteSerializedMessageAsync(new GetMinimalExtensionVersion()).ConfigureAwait(false);
                var response = await client.GetStream().ReadSerializedMessageAsync<IResponse>().ConfigureAwait(false);

                if (response == null)
                {
                    ConnectionState = ClientState.Disconnected;
                    throw new UnsupportedServerVersionException(ConnectionOptions, Constants.MinimalRequiredServerVersion);
                }

                var minimalExtVersion = ((MinimalExtensionVersion)response).MinExtensionVersion;
                var serverVersion = ((MinimalExtensionVersion)response).ServerVersion;
                if (string.Compare(minimalExtVersion, _extensionVersion, comparisonType: StringComparison.OrdinalIgnoreCase) > 0)
                {
                    ConnectionState = ClientState.Disconnected;
                    throw new UnsupportedDebuggerVersionException(ConnectionOptions, minimalExtVersion);
                }

                if (string.Compare(Constants.MinimalRequiredServerVersion, serverVersion, comparisonType: StringComparison.OrdinalIgnoreCase) > 0)
                {
                    ConnectionState = ClientState.Disconnected;
                    throw new UnsupportedServerVersionException(ConnectionOptions, Constants.MinimalRequiredServerVersion);
                }

                _connection = client;
                ConnectionState = ClientState.Connected;
            }
            catch (Exception e) when (!(e is UnsupportedDebuggerVersionException) && !(e is UnsupportedServerVersionException))
            {
                ConnectionState = ClientState.Disconnected;
                throw new ConnectionRefusedException(ConnectionOptions);
            }
        }
    }
}
