using Microsoft.VisualStudio.Debugger.Interop;
using Moq;
using System;

namespace VSRAD.DeborgarTests
{
    static class Helpers
    {
        public delegate int EventCallback(IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram,
            IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

        public static Mock<IDebugEventCallback2> MakeCallbackMock(EventCallback callback)
        {
            var debugCallbackMock = new Mock<IDebugEventCallback2>();
            debugCallbackMock
                .Setup(cb => cb.Event(It.IsAny<IDebugEngine2>(), It.IsAny<IDebugProcess2>(),
                    It.IsAny<IDebugProgram2>(), It.IsAny<IDebugThread2>(), It.IsAny<IDebugEvent2>(),
                    ref It.Ref<Guid>.IsAny, It.IsAny<uint>()))
                .Returns(callback);
            return debugCallbackMock;
        }
    }
}
