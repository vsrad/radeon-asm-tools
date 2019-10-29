using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace VSRAD.Deborgar
{
    public interface IEngineCallbacks
    {
        void OnAttach();
        void OnBreakpointBound(Breakpoint breakpoint);
        void OnBreakpointHit(IDebugBoundBreakpoint2 breakpoint);
        void OnProgramTerminated();
        void OnStepComplete();
        void OnBreakComplete();
    }

    public sealed class EngineCallbacks : IEngineCallbacks
    {
        private readonly DebugEngine _engine;
        private readonly Program _program;
        private readonly IDebugEventCallback2 _ad7Callback;
        private readonly IDebugProcess2 _process;

        public EngineCallbacks(DebugEngine engine, Program program, IDebugProcess2 process, IDebugEventCallback2 ad7Callback)
        {
            _engine = engine;
            _program = program;
            _ad7Callback = ad7Callback;
            _process = process;
        }

        public void OnAttach()
        {
            Send(new AD7EngineCreateEvent(_engine), AD7EngineCreateEvent.GUID);
            Send(new AD7ProgramCreateEvent(), AD7ProgramCreateEvent.GUID);
            Send(new AD7LoadCompleteEvent(), AD7LoadCompleteEvent.GUID);
        }

        public void OnBreakpointBound(Breakpoint breakpoint)
        {
            Send(new AD7BreakpointBoundEvent(breakpoint), AD7BreakpointBoundEvent.GUID);
        }

        public void OnBreakpointHit(IDebugBoundBreakpoint2 breakpoint)
        {
            var boundBreakpoints = new AD7BoundBreakpointsEnum(new[] { breakpoint });
            Send(new AD7BreakpointEvent(boundBreakpoints), AD7BreakpointEvent.GUID);
        }

        public void OnProgramTerminated()
        {
            Send(new AD7ProgramDestroyEvent(), AD7ProgramDestroyEvent.GUID);
        }

        public void OnStepComplete()
        {
            Send(new AD7StepCompleteEvent(), AD7StepCompleteEvent.GUID);
        }

        public void OnBreakComplete()
        {
            Send(new AD7BreakCompleteEvent(), AD7BreakCompleteEvent.GUID);
        }

        private void Send(IDebugEvent2 eventObject, Guid eventGuid)
        {
            ErrorHandler.ThrowOnFailure(eventObject.GetAttributes(out var attributes));
            ErrorHandler.ThrowOnFailure(_ad7Callback.Event(
                _engine, _process, _program, _program, eventObject, eventGuid, attributes));
        }
    }
}
