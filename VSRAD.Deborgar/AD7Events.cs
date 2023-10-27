using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace VSRAD.Deborgar
{
    #region Event base classes

    public class AD7AsynchronousEvent : IDebugEvent2
    {
        public const uint Attributes = (uint) enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS;

        int IDebugEvent2.GetAttributes(out uint eventAttributes)
        {
            eventAttributes = Attributes;
            return VSConstants.S_OK;
        }
    }

    public class AD7StoppingEvent : IDebugEvent2
    {
        public const uint Attributes = (uint) enum_EVENTATTRIBUTES.EVENT_ASYNC_STOP;

        int IDebugEvent2.GetAttributes(out uint eventAttributes)
        {
            eventAttributes = Attributes;
            return VSConstants.S_OK;
        }
    }

    public class AD7SynchronousEvent : IDebugEvent2
    {
        public const uint Attributes = (uint) enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS;

        int IDebugEvent2.GetAttributes(out uint eventAttributes)
        {
            eventAttributes = Attributes;
            return VSConstants.S_OK;
        }
    }

    public class AD7SynchronousStoppingEvent : IDebugEvent2
    {
        public const uint Attributes = (uint) enum_EVENTATTRIBUTES.EVENT_SYNC_STOP;

        int IDebugEvent2.GetAttributes(out uint eventAttributes)
        {
            eventAttributes = Attributes;
            return VSConstants.S_OK;
        }
    }

    #endregion

    // The debug engine (DE) sends this interface to the session debug manager (SDM) when an instance of the DE is created.
    public sealed class AD7EngineCreateEvent : AD7AsynchronousEvent, IDebugEngineCreateEvent2
    {
        public static readonly Guid GUID = typeof(IDebugEngineCreateEvent2).GUID;

        private readonly IDebugEngine2 _engine;

        public AD7EngineCreateEvent(IDebugEngine2 engine)
        {
            _engine = engine;
        }

        int IDebugEngineCreateEvent2.GetEngine(out IDebugEngine2 engine)
        {
            engine = _engine;
            return VSConstants.S_OK;
        }
    }

    public sealed class AD7ProgramCreateEvent : AD7AsynchronousEvent, IDebugProgramCreateEvent2
    {
        public static readonly Guid GUID = typeof(IDebugProgramCreateEvent2).GUID;
    }

    public sealed class AD7ProgramDestroyEvent : AD7SynchronousEvent, IDebugProgramDestroyEvent2
    {
        public static readonly Guid GUID = typeof(IDebugProgramDestroyEvent2).GUID;

        int IDebugProgramDestroyEvent2.GetExitCode(out uint exitCode)
        {
            exitCode = 0;
            return VSConstants.S_OK;
        }
    }

    public sealed class AD7ThreadCreateEvent : AD7AsynchronousEvent, IDebugThreadCreateEvent2
    {
        public static readonly Guid GUID = typeof(IDebugThreadCreateEvent2).GUID;
    }

    public sealed class AD7ThreadDestroyEvent : AD7AsynchronousEvent, IDebugThreadDestroyEvent2
    {
        public static readonly Guid GUID = typeof(IDebugThreadDestroyEvent2).GUID;

        int IDebugThreadDestroyEvent2.GetExitCode(out uint exitCode)
        {
            exitCode = 0;
            return VSConstants.S_OK;
        }
    }

    public sealed class AD7LoadCompleteEvent : AD7SynchronousEvent, IDebugLoadCompleteEvent2
    {
        public static readonly Guid GUID = typeof(IDebugLoadCompleteEvent2).GUID;
    }

    public sealed class AD7StepCompleteEvent : AD7StoppingEvent, IDebugStepCompleteEvent2
    {
        public static readonly Guid GUID = typeof(IDebugStepCompleteEvent2).GUID;
    }

    public sealed class AD7BreakCompleteEvent : AD7StoppingEvent, IDebugBreakEvent2
    {
        public static readonly Guid GUID = typeof(IDebugBreakEvent2).GUID;
    }
}
