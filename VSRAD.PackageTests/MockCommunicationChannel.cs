using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests
{
    class MockCommunicationChannel
    {
        public ICommunicationChannel Object => _mock.Object;

        private readonly Mock<ICommunicationChannel> _mock;

        private readonly Queue<(IResponse response, Action<ICommand> callback)> _replyInteractions =
            new Queue<(IResponse response, Action<ICommand> callback)>();

        private readonly Queue<Action<ICommand>> _nonReplyInteractions =
            new Queue<Action<ICommand>>();

        public bool AllInteractionsHandled => _replyInteractions.Count == 0 && _nonReplyInteractions.Count == 0;

        public MockCommunicationChannel()
        {
            _mock = new Mock<ICommunicationChannel>();
            _mock
                .Setup((c) => c.SendWithReplyAsync<ExecutionCompleted>(It.IsAny<ICommand>()))
                .Returns<ICommand>((c) => Task.FromResult((ExecutionCompleted)HandleCommand(c, withReply: true)));
            _mock
                .Setup((c) => c.SendWithReplyAsync<MetadataFetched>(It.IsAny<ICommand>()))
                .Returns<ICommand>((c) => Task.FromResult((MetadataFetched)HandleCommand(c, withReply: true)));
            _mock
                .Setup((c) => c.SendWithReplyAsync<ResultRangeFetched>(It.IsAny<ICommand>()))
                .Returns<ICommand>((c) => Task.FromResult((ResultRangeFetched)HandleCommand(c, withReply: true)));
            _mock
                .Setup((c) => c.SendAsync(It.IsAny<ICommand>()))
                .Returns<ICommand>((c) =>
                {
                    HandleCommand(c, withReply: false);
                    return Task.CompletedTask;
                });
        }

        public void ThenRespond<TCommand, TResponse>(TResponse response, Action<TCommand> processCallback)
            where TCommand : ICommand where TResponse : IResponse =>
            _replyInteractions.Enqueue((response, (c) => processCallback((TCommand)c)));

        public void ThenRespond<TResponse>(TResponse response)
            where TResponse : IResponse =>
            _replyInteractions.Enqueue((response, null));

        public void ThenExpect<TCommand>(Action<TCommand> processCallback)
            where TCommand : ICommand =>
            _nonReplyInteractions.Enqueue((c) => processCallback((TCommand)c));

        public void ThenExpect<TCommand>()
            where TCommand : ICommand =>
            _nonReplyInteractions.Enqueue((c) => Assert.IsType<TCommand>(c));

        private IResponse HandleCommand(ICommand command, bool withReply)
        {
            if (withReply)
            {
                if (_replyInteractions.Count == 0)
                {
                    throw new Xunit.Sdk.XunitException("The test class has sent a request (and is waiting for a reply) when none was expected.");
                }
                var (response, callback) = _replyInteractions.Dequeue();
                callback?.Invoke(command);
                return response;
            }
            else
            {
                if (_nonReplyInteractions.Count == 0)
                {
                    throw new Xunit.Sdk.XunitException("The test class has sent a request (without waiting for a reply) when none was expected.");
                }
                _nonReplyInteractions.Dequeue()?.Invoke(command);
                return null;
            }
        }
    }
}
