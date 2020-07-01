using System.IO.Pipes;

namespace VSRAD.BuildTools
{
    public sealed class IPCBridge
    {
        private const int _pipeConnectionTimeout = 1000; // 1s

        private readonly NamedPipeClientStream _pipe;

        public IPCBridge(string project)
        {
            _pipe = new NamedPipeClientStream(".", IPCBuildResult.GetIPCPipeName(project), PipeDirection.In, PipeOptions.Asynchronous);
        }

        public IPCBuildResult Build()
        {
            _pipe.Connect(_pipeConnectionTimeout);
            var result = IPCBuildResult.Read(_pipe);
            _pipe.Close();
            return result;
        }
    }
}
