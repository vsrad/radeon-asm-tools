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

        Task SendAsync(ICommand command);

        Task<T> SendWithReplyAsync<T>(ICommand command) where T : IResponse;

        Task<IResponse[]> SendBundleAsync(List<ICommand> commands);

        Task<IReadOnlyDictionary<string, string>> GetRemoteEnvironmentAsync();

        void ForceDisconnect();
    }

    public sealed class ConnectionRefusedException : System.IO.IOException
    {
        public ConnectionRefusedException(ServerConnectionOptions connection) :
            base($"Unable to establish connection to a debug server at {connection}")
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
        public ServerConnectionOptions ConnectionOptions => _project.Options.Profile.General.Connection;

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

        private readonly OutputWindowWriter _outputWindowWriter;
        private readonly IProject _project;

        private TcpClient _connection;
        private IReadOnlyDictionary<string, string> _remoteEnvironment;

        [ImportingConstructor]
        public CommunicationChannel(SVsServiceProvider provider, IProject project)
        {
            _outputWindowWriter = new OutputWindowWriter(provider,
                Constants.OutputPaneServerGuid, Constants.OutputPaneServerTitle);
            _project = project;
            _project.Loaded += (options) =>
                options.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(options.ActiveProfile)) ForceDisconnect(); };
        }

        public async Task SendAsync(ICommand command)
        {
            await EstablishServerConnectionAsync().ConfigureAwait(false);
            try
            {
                await _connection.GetStream().WriteSerializedMessageAsync(command).ConfigureAwait(false);
                await _outputWindowWriter.PrintMessageAsync($"Sent command to {ConnectionOptions}", command.ToString()).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) // ForceDisconnect has been called within the try block 
            {
                throw new OperationCanceledException();
            }
            catch (Exception e)
            {
                ForceDisconnect();
                throw new Exception($"Connection to {ConnectionOptions} has been terminated: {e.Message}");
            }
        }

        public async Task<T> SendWithReplyAsync<T>(ICommand command) where T : IResponse
        {
            await SendAsync(command).ConfigureAwait(false);
            try
            {
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
                ForceDisconnect();
                throw new Exception($"Connection to {ConnectionOptions} has been terminated: {e.Message}");
            }
        }

        public Task<IResponse[]> SendBundleAsync(List<ICommand> commands)
        {
            throw new NotImplementedException();
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
                    _connection = client;
                    ConnectionState = ClientState.Connected;
                }
            }
            catch (Exception)
            {
                ConnectionState = ClientState.Disconnected;
                throw new ConnectionRefusedException(ConnectionOptions);
            }
        }
    }
}
