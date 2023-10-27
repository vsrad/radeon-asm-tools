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
        [Fact]
        public void TestAttachSequence()
        {
            var integrationMock = new Mock<IEngineIntegration>();
            var initMock = new Mock<DebugEngineInitialization>();
            initMock.Setup(i => i()).Returns(integrationMock.Object);
            DebugEngine.InitializationCallback = initMock.Object;

            var processMock = new Mock<IDebugProcess2>();
            var process = processMock.Object;

            var program = new DebugProgram(process);
            var engine = new DebugEngine();

            bool engineCreateRaised = false, programCreateRaised = false, loadCompleteRaised = false;

            var callbackMock = Helpers.MakeCallbackMock(
                (IDebugEngine2 argEngine, IDebugProcess2 argProcess, IDebugProgram2 argProgram, IDebugThread2 argThread, IDebugEvent2 argEvent, ref Guid argGuid, uint argAttr) =>
                {
                    if (!engineCreateRaised && !programCreateRaised && !loadCompleteRaised)
                    {
                        Assert.IsType<AD7EngineCreateEvent>(argEvent);
                        Assert.Equal(AD7EngineCreateEvent.GUID, argGuid);
                        ((IDebugEngineCreateEvent2)argEvent).GetEngine(out var eventEngine);
                        Assert.Equal(engine, eventEngine);
                        engineCreateRaised = true;
                    }
                    else if (engineCreateRaised && !programCreateRaised && !loadCompleteRaised)
                    {
                        Assert.IsType<AD7ProgramCreateEvent>(argEvent);
                        Assert.Equal(AD7ProgramCreateEvent.GUID, argGuid);
                        programCreateRaised = true;
                    }
                    else if (engineCreateRaised && programCreateRaised && !loadCompleteRaised)
                    {
                        Assert.IsType<AD7LoadCompleteEvent>(argEvent);
                        Assert.Equal(AD7LoadCompleteEvent.GUID, argGuid);
                        loadCompleteRaised = true;
                    }
                    return VSConstants.S_OK;
                });

            engine.Attach(new IDebugProgram2[] { program }, null, 0,
                callbackMock.Object, enum_ATTACH_REASON.ATTACH_REASON_LAUNCH);

            initMock.Verify(init => init(), Times.Once);
            Assert.True(engineCreateRaised && programCreateRaised && loadCompleteRaised);
        }
    }
}
