using System;

namespace VSRAD.DebugServer
{
    internal sealed class ConnectionFailedException : Exception
    {
        public ConnectionFailedException()
            : base("Client has been disconnected") { }

        public ConnectionFailedException(string message)
            : base(message) { }
    }
}
