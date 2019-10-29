using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Moq;
using System;
using VSRAD.Deborgar;
using Xunit;

namespace VSRAD.DeborgarTests
{
    public class DebugEngineTests
    {
        delegate int EventCallback(IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram,
            IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib);

        [Fact]
        public void TestAttachSequence()
        {
            var integrationMock = new Mock<IEngineIntegration>();
            var initMock = new Mock<DebugEngineInitialization>();
            initMock.Setup(i => i()).Returns(integrationMock.Object);
            DebugEngine.InitializationCallback = initMock.Object;

            var processMock = new Mock<IDebugProcess2>();
            var process = processMock.Object;

            var program = new Program(process);
            var engine = new DebugEngine();

            bool engineCreateRaised = false, programCreateRaised = false, loadCompleteRaised = false;

            var callbackMock = new Mock<IDebugEventCallback2>();
            callbackMock
                .Setup(cb => cb.Event(engine, process,
                    It.IsAny<IDebugProgram2>(), It.IsAny<IDebugThread2>(), It.IsAny<IDebugEvent2>(),
                    ref It.Ref<Guid>.IsAny, It.IsAny<uint>()))
                .Returns(new EventCallback(
                    (IDebugEngine2 argEngine, IDebugProcess2 argProcess, IDebugProgram2 argProgram,
                     IDebugThread2 argThread, IDebugEvent2 argEvent, ref Guid argGuid, uint argAttr) =>
                {
                    if (!engineCreateRaised && !programCreateRaised && !loadCompleteRaised)
                    {
                        if (argEvent is AD7EngineCreateEvent)
                        {
                            Assert.Equal(AD7EngineCreateEvent.GUID, argGuid);
                            ((IDebugEngineCreateEvent2)argEvent).GetEngine(out var eventEngine);
                            Assert.Equal(engine, eventEngine);
                            engineCreateRaised = true;
                        }
                        else Assert.True(false, "Expected IDebugEngineCreateEvent2");
                    }
                    else if (engineCreateRaised && !programCreateRaised && !loadCompleteRaised)
                    {
                        if (argEvent is AD7ProgramCreateEvent)
                        {
                            Assert.Equal(AD7ProgramCreateEvent.GUID, argGuid);
                            programCreateRaised = true;
                        }
                        else Assert.True(false, "Expected IDebugProgramCreateEvent2");
                    }
                    else if (engineCreateRaised && programCreateRaised && !loadCompleteRaised)
                    {
                        if (argEvent is AD7LoadCompleteEvent)
                        {
                            Assert.Equal(AD7LoadCompleteEvent.GUID, argGuid);
                        }
                        else Assert.True(false, "Expected IDebugLoadCompleteEvent2");
                    }

                    return VSConstants.S_OK;
                }));

            engine.Attach(new IDebugProgram2[] { program }, null, 0,
                callbackMock.Object, enum_ATTACH_REASON.ATTACH_REASON_LAUNCH);

            initMock.Verify(init => init(), Times.Once);
        }
    }
}
