﻿using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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

        Task<T> SendWithReplyAsync<T>(ICommand command, CancellationToken cancellationToken) where T : IResponse;

        Task<OSPlatform> GetRemotePlatformAsync(CancellationToken cancellationToken);

        Task<IReadOnlyDictionary<string, string>> GetRemoteEnvironmentAsync(CancellationToken cancellationToken);

        void ForceDisconnect();
    }

    public class ConnectionFailedException : UserException
    {
        public ConnectionFailedException(ServerConnectionOptions connection) : base($"Failed to connect to the debug server at {connection}") { }
        public ConnectionFailedException(string message) : base(message) { }
    }

    public sealed class UnsupportedServerVersionException : ConnectionFailedException
    {
        public UnsupportedServerVersionException(ServerConnectionOptions connection, Version serverVersion) :
            base($"The debug server at {connection} is out of date and missing critical features. Please update it to the {serverVersion} or above version.")
        { }
    }

    public sealed class UnsupportedExtensionVersionException : ConnectionFailedException
    {
        public UnsupportedExtensionVersionException(ServerConnectionOptions connection, Version serverVersion) :
            base($"This extension is out of date and missing critical features to work with the debug server at {connection}. Please update the extension to the {serverVersion} or above version.")
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

        private static readonly TimeSpan _connectionTimeout = new TimeSpan(hours: 0, minutes: 0, seconds: 5);

        private readonly SemaphoreSlim _sendMutex = new SemaphoreSlim(1);
        private readonly OutputWindowWriter _outputWindowWriter;
        private readonly IProject _project;

        private Version _extensionVersion;
        private TcpClient _connection;
        private OSPlatform _remotePlatform;
        private IReadOnlyDictionary<string, string> _remoteEnvironment;

        [ImportingConstructor]
        public CommunicationChannel(SVsServiceProvider serviceProvider, IProject project)
        {
            _outputWindowWriter = new OutputWindowWriter(serviceProvider,
                Constants.OutputPaneServerGuid, Constants.OutputPaneServerTitle);
            _project = project;
            _project.RunWhenLoaded((options) =>
            {
                _extensionVersion = VSPackage.GetExtensionVersion(serviceProvider);
                options.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(options.ActiveProfile)) ForceDisconnect(); };
            });
        }

        public async Task<T> SendWithReplyAsync<T>(ICommand command, CancellationToken cancellationToken) where T : IResponse
        {
            await _sendMutex.WaitAsync(cancellationToken);
            try
            {
                return await SendWithReplyAsync<T>(command, reconnectOnError: true, cancellationToken);
            }
            finally
            {
                _sendMutex.Release();
            }
        }

        private async Task<T> SendWithReplyAsync<T>(ICommand command, bool reconnectOnError, CancellationToken cancellationToken) where T : IResponse
        {
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
            catch (Exception e) when (!(e is ConnectionFailedException))
            {
                ForceDisconnect(); // At this point, the stream may be corrupted while we are still connected (e.g. in case of EndOfStreamException), so close the connection first
                if (reconnectOnError)
                {
                    await _outputWindowWriter.PrintMessageAsync($"Connection to {ConnectionOptions} lost, attempting to reconnect...").ConfigureAwait(false);
                    return await SendWithReplyAsync<T>(command, reconnectOnError: false, cancellationToken);
                }
                else
                {
                    await _outputWindowWriter.PrintMessageAsync($"Could not reconnect to {ConnectionOptions}").ConfigureAwait(false);
                    throw new UserException($"Connection to {ConnectionOptions} has been terminated: {e.Message}");
                }
            }
        }

        public async Task<OSPlatform> GetRemotePlatformAsync(CancellationToken cancellationToken)
        {
            await EstablishServerConnectionAsync(cancellationToken).ConfigureAwait(false);
            return _remotePlatform;
        }

        public async Task<IReadOnlyDictionary<string, string>> GetRemoteEnvironmentAsync(CancellationToken cancellationToken)
        {
            if (_remoteEnvironment == null)
            {
                var environment = await SendWithReplyAsync<EnvironmentVariablesListed>(new ListEnvironmentVariables(), cancellationToken).ConfigureAwait(false);
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

        private async Task EstablishServerConnectionAsync(CancellationToken cancellationToken)
        {
            if (_connection != null && _connection.Connected) return;

            ConnectionState = ClientState.Connecting;
            var client = new TcpClient();
            try
            {
                using (var timeoutCts = new CancellationTokenSource(_connectionTimeout))
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
                using (cts.Token.Register(() => client.Dispose()))
                {
                    await client.ConnectAsync(ConnectionOptions.RemoteMachine, ConnectionOptions.Port).ConfigureAwait(false);
                    _connection = client;
                    ConnectionState = ClientState.Connected;
                }
            }
            catch (Exception)
            {
                client.Dispose();
                ConnectionState = ClientState.Disconnected;
                throw new ConnectionFailedException(ConnectionOptions);
            }

            try
            {
                var exchangeVersionsCommand = new ExchangeVersionsCommand { ClientPlatform = OSPlatform.Windows, ClientVersion = _extensionVersion };
                var versionResponse = await SendWithReplyAsync<ExchangeVersionsResponse>(exchangeVersionsCommand, reconnectOnError: false, cancellationToken).ConfigureAwait(false);
                if (versionResponse.Status == ExchangeVersionsStatus.ClientVersionUnsupported)
                    throw new UnsupportedExtensionVersionException(ConnectionOptions, versionResponse.ServerVersion);
                if (versionResponse.ServerVersion < Constants.MinimalRequiredServerVersion)
                    throw new UnsupportedServerVersionException(ConnectionOptions, Constants.MinimalRequiredServerVersion);
                _remotePlatform = versionResponse.ServerPlatform;
            }
            catch (Exception)
            {
                ForceDisconnect();
                throw;
            }
        }
    }
}
