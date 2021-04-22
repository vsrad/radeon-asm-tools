using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.Server;

namespace VSRAD.PackageTests
{
    class MockCommunicationChannel : ICommunicationChannel
    {
        private readonly Queue<(IResponse response, Action<ICommand> callback)> _interactions =
            new Queue<(IResponse response, Action<ICommand> callback)>();
        private readonly CapabilityInfo _capabilityInfo;

        public event Action ConnectionStateChanged;

        public bool AllInteractionsHandled => _interactions.Count == 0;

        public ServerConnectionOptions ConnectionOptions => throw new NotImplementedException();

        public ClientState ConnectionState => throw new NotImplementedException();

        public void RaiseConnectionStateChanged() => ConnectionStateChanged();

        public MockCommunicationChannel(ServerPlatform platform = ServerPlatform.Windows)
        {
            _capabilityInfo = new CapabilityInfo("", platform, CapabilityInfo.LatestServerCapabilities);
        }

        public void ThenRespond<TCommand, TResponse>(TResponse response, Action<TCommand> processCallback)
            where TCommand : ICommand where TResponse : IResponse =>
            _interactions.Enqueue((response, (c) => processCallback((TCommand)c)));

        public void ThenRespond<TResponse>(TResponse response)
            where TResponse : IResponse =>
            _interactions.Enqueue((response, null));

        public Task<T> SendWithReplyAsync<T>(ICommand command, CancellationToken cancellationToken) where T : IResponse
        {
            if (_interactions.Count == 0)
            {
                throw new Xunit.Sdk.XunitException("The test method has sent a request (and is waiting for a reply) when none was expected.");
            }
            var (response, callback) = _interactions.Dequeue();
            callback?.Invoke(command);
            return Task.FromResult((T)response);
        }

        public Task<IReadOnlyDictionary<string, string>> GetRemoteEnvironmentAsync()
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }

        public Task<CapabilityInfo> GetServerCapabilityInfoAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_capabilityInfo);
        }

        public void ForceDisconnect()
        {
            throw new NotImplementedException();
        }
    }
}
