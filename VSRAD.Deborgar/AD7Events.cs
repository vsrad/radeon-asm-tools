using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace VSRAD.Deborgar
{
    public class AD7Event : IDebugEvent2
    {
        public Guid GUID { get; }

        private readonly enum_EVENTATTRIBUTES _eventAttributes;

        public AD7Event(Guid eventGuid, enum_EVENTATTRIBUTES eventAttributes)
        {
            GUID = eventGuid;
            _eventAttributes = eventAttributes;
        }

        public int GetAttributes(out uint pdwAttrib)
        {
            pdwAttrib = (uint)_eventAttributes;
            return VSConstants.S_OK;
        }
    }

    public sealed class AD7EngineCreateEvent : AD7Event, IDebugEngineCreateEvent2
    {
        private readonly IDebugEngine2 _engine;

        public AD7EngineCreateEvent(IDebugEngine2 engine)
            : base(typeof(IDebugEngineCreateEvent2).GUID, enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS)
        {
            _engine = engine;
        }

        int IDebugEngineCreateEvent2.GetEngine(out IDebugEngine2 engine)
        {
            engine = _engine;
            return VSConstants.S_OK;
        }
    }

    public sealed class AD7ProgramCreateEvent : AD7Event, IDebugProgramCreateEvent2
    {
        public AD7ProgramCreateEvent()
            : base(typeof(IDebugProgramCreateEvent2).GUID, enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS)
        {
        }
    }

    public sealed class AD7ProgramDestroyEvent : AD7Event, IDebugProgramDestroyEvent2
    {
        public AD7ProgramDestroyEvent()
            : base(typeof(IDebugProgramDestroyEvent2).GUID, enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS)
        {
        }

        int IDebugProgramDestroyEvent2.GetExitCode(out uint exitCode)
        {
            exitCode = 0;
            return VSConstants.S_OK;
        }
    }

    public sealed class AD7ThreadCreateEvent : AD7Event, IDebugThreadCreateEvent2
    {
        public AD7ThreadCreateEvent()
            : base(typeof(IDebugThreadCreateEvent2).GUID, enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS | enum_EVENTATTRIBUTES.EVENT_IMMEDIATE)
        {
        }
    }

    public sealed class AD7ThreadDestroyEvent : AD7Event, IDebugThreadDestroyEvent2
    {
        public AD7ThreadDestroyEvent()
            : base(typeof(IDebugThreadDestroyEvent2).GUID, enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS)
        {
        }

        int IDebugThreadDestroyEvent2.GetExitCode(out uint exitCode)
        {
            exitCode = 0;
            return VSConstants.S_OK;
        }
    }

    public sealed class AD7LoadCompleteEvent : AD7Event, IDebugLoadCompleteEvent2
    {
        public AD7LoadCompleteEvent()
            : base(typeof(IDebugLoadCompleteEvent2).GUID, enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS)
        {
        }
    }

    public sealed class AD7StepCompleteEvent : AD7Event, IDebugStepCompleteEvent2
    {
        public AD7StepCompleteEvent()
            : base(typeof(IDebugStepCompleteEvent2).GUID, enum_EVENTATTRIBUTES.EVENT_SYNC_STOP | enum_EVENTATTRIBUTES.EVENT_IMMEDIATE)
        {
        }
    }

    public sealed class AD7BreakCompleteEvent : AD7Event, IDebugBreakEvent2
    {
        public AD7BreakCompleteEvent()
            : base(typeof(IDebugBreakEvent2).GUID, enum_EVENTATTRIBUTES.EVENT_SYNC_STOP | enum_EVENTATTRIBUTES.EVENT_IMMEDIATE)
        {
        }
    }
}
