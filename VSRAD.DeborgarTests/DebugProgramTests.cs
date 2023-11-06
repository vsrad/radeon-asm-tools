using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Moq;
using System;
using VSRAD.Deborgar;
using Xunit;

namespace VSRAD.DeborgarTests
{
    public class DebugProgramTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TestBreak(bool step)
        {
            var integrationMock = new Mock<IEngineIntegration>();
            integrationMock.Setup((i) => i.Execute(step)).Callback(() =>
                integrationMock.Raise((i) => i.ExecutionCompleted += null, null,
                new ExecutionCompletedEventArgs(new[] { new BreakInstance(1, new[] { ("", "h.s", 7u) }) }, isStepping: step, isSuccessful: true)));

            bool loadCompleteRaised = false, threadCreatedRaised = false, breakRaised = false;
            DebugThread debugThread = null;

            var callbackMock = Helpers.MakeCallbackMock(
                (IDebugEngine2 argEngine, IDebugProcess2 argProcess, IDebugProgram2 argProgram, IDebugThread2 argThread, IDebugEvent2 argEvent, ref Guid argGuid, uint argAttr) =>
                {
                    if (!loadCompleteRaised)
                    {
                        loadCompleteRaised = argEvent is AD7LoadCompleteEvent;
                    }
                    else if (!threadCreatedRaised)
                    {
                        Assert.IsType<AD7ThreadCreateEvent>(argEvent);
                        Assert.Equal(typeof(IDebugThreadCreateEvent2).GUID, argGuid);
                        debugThread = (DebugThread)argThread;
                        threadCreatedRaised = true;
                    }
                    else if (!breakRaised)
                    {
                        if (step)
                        {
                            Assert.IsType<AD7StepCompleteEvent>(argEvent);
                            Assert.Equal(typeof(IDebugStepCompleteEvent2).GUID, argGuid);
                        }
                        else
                        {
                            Assert.IsType<AD7BreakCompleteEvent>(argEvent);
                            Assert.Equal(typeof(IDebugBreakEvent2).GUID, argGuid);
                        }
                        Assert.Equal(debugThread, argThread);
                        breakRaised = true;
                    }
                    return VSConstants.S_OK;
                });

            var (program, engineMock) = (new DebugProgram(null), new Mock<IDebugEngine2>());
            program.AttachDebugger(engineMock.Object, callbackMock.Object, integrationMock.Object);

            program.Execute(step);
            integrationMock.Verify((i) => i.Execute(step), Times.Once);
            Assert.True(loadCompleteRaised && threadCreatedRaised && breakRaised);

            Assert.Equal(VSConstants.S_OK, debugThread.EnumFrameInfo(enum_FRAMEINFO_FLAGS.FIF_ARGS_ALL, nRadix: 16, out var frameEnum));
            var frameInfo = new FRAMEINFO[1];
            uint fetched = 0;
            Assert.Equal(VSConstants.S_OK, frameEnum.Next(1, frameInfo, ref fetched));
            var frame = frameInfo[0].m_pFrame;
            Assert.Equal(VSConstants.S_OK, frame.GetDocumentContext(out var context));
            Assert.Equal(VSConstants.S_OK, context.GetName(default, out var documentName));
            var position = new TEXT_POSITION[1];
            Assert.Equal(VSConstants.S_OK, context.GetStatementRange(position, position));

            Assert.Equal("h.s", documentName);
            Assert.Equal(7u, position[0].dwLine);
        }
    }
}
