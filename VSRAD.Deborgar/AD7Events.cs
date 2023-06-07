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
        public static readonly Guid GUID = new Guid("FE5B734C-759D-4E59-AB04-F103343BDD06");

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
        public static readonly Guid GUID = new Guid("96CD11EE-ECD4-4E89-957E-B5D496FC4139");
    }

    sealed class AD7ProgramDestroyEvent : AD7SynchronousEvent, IDebugProgramDestroyEvent2
    {
        public static readonly Guid GUID = new Guid("E147E9E3-6440-4073-A7B7-A65592C714B5");

        int IDebugProgramDestroyEvent2.GetExitCode(out uint exitCode)
        {
            exitCode = 0;
            return VSConstants.S_OK;
        }
    }

    public sealed class AD7LoadCompleteEvent : AD7SynchronousEvent, IDebugLoadCompleteEvent2
    {
        public static readonly Guid GUID = new Guid("B1844850-1349-45D4-9F12-495212F5EB0B");
    }

    sealed class AD7StepCompleteEvent : AD7StoppingEvent, IDebugStepCompleteEvent2
    {
        public static readonly Guid GUID = new Guid("0F7F24C1-74D9-4EA6-A3EA-7EDB2D81441D");
    }
          
    sealed class AD7BreakCompleteEvent : AD7StoppingEvent, IDebugBreakEvent2
    {
        public static readonly Guid GUID = new Guid("c7405d1d-e24b-44e0-b707-d8a5a4e1641b");
    }
}
