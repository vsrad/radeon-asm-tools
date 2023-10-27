using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Runtime.InteropServices;

namespace VSRAD.Deborgar
{
    public delegate IEngineIntegration DebugEngineInitialization();
    public delegate void DebugEngineTermination();

    [ComVisible(true)]
    [Guid(Constants.DebugEngineId)]
    public sealed class DebugEngine : IDebugEngine2, IDebugEngineLaunch2
    {
        /* We use static delegates to access the integrating project system (VSRAD.Package) --
         * MEF [Import]s don't work here because this class is intantiated by older Visual Studio APIs. */
        public static DebugEngineInitialization InitializationCallback;
        public static DebugEngineTermination TerminationCallback;

        private DebugProgram _program;

        public int Attach(IDebugProgram2[] programs, IDebugProgramNode2[] programNodes, uint celtPrograms, IDebugEventCallback2 ad7Callback, enum_ATTACH_REASON dwReason)
        {
            _program = programs[0] as DebugProgram;
            _program.AttachDebugger(this, ad7Callback, InitializationCallback());
            return VSConstants.S_OK;
        }

        public int ContinueFromSynchronousEvent(IDebugEvent2 eventObject)
        {
            switch (eventObject)
            {
                case AD7ProgramDestroyEvent _:
                    _program = null;
                    TerminationCallback();
                    break;
                case AD7LoadCompleteEvent _:
                    _program.Execute(step: false);
                    break;
            }
            return VSConstants.S_OK;
        }

        int IDebugEngine2.DestroyProgram(IDebugProgram2 program)
        {
            ErrorHandler.ThrowOnFailure(program.Terminate());
            return VSConstants.S_OK;
        }

        int IDebugEngine2.GetEngineId(out Guid guidEngine)
        {
            guidEngine = Constants.DebugEngineGuid;
            return VSConstants.S_OK;
        }

        int IDebugEngineLaunch2.CanTerminateProcess(IDebugProcess2 process) => VSConstants.S_OK;

        int IDebugEngineLaunch2.TerminateProcess(IDebugProcess2 process) => process.Terminate();

        // Requests that the program stops execution the next time one of their threads attempts to run.
        // This is normally called in response to the user clicking on the pause button in the debugger.
        int IDebugEngine2.CauseBreak() => VSConstants.E_NOTIMPL;

        public int LaunchSuspended(string pszServer, IDebugPort2 port, string serverExe, string serverArgs, string dir, string env, string opts, enum_LAUNCH_FLAGS launchFlags, uint hStdInput, uint hStdOutput, uint hStdError, IDebugEventCallback2 ad7Callback, out IDebugProcess2 process) =>
            throw new NotImplementedException("Launching a local process is not supported. The engine must be launched with DebugLaunchOperation.AlreadyRunning");

        public int ResumeProcess(IDebugProcess2 process) =>
            throw new NotImplementedException("Launching a local process is not supported. The engine must be launched with DebugLaunchOperation.AlreadyRunning");

        #region Unsupported

        int IDebugEngine2.RemoveAllSetExceptions(ref Guid guidType) => VSConstants.S_OK;

        int IDebugEngine2.RemoveSetException(EXCEPTION_INFO[] pException) => VSConstants.S_OK;

        int IDebugEngine2.SetException(EXCEPTION_INFO[] pException) => VSConstants.S_OK;

        int IDebugEngine2.SetLocale(ushort wLangID) => VSConstants.S_OK;

        int IDebugEngine2.SetMetric(string pszMetric, object varValue) => VSConstants.S_OK;

        int IDebugEngine2.SetRegistryRoot(string pszRegistryRoot) => VSConstants.S_OK;

        int IDebugEngine2.EnumPrograms(out IEnumDebugPrograms2 programs) =>
            throw new NotImplementedException(nameof(IDebugEngine2.EnumPrograms) + " is a deprecated method not called by the debugger.");

        int IDebugEngine2.CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP)
        {
            ppPendingBP = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion
    }
}
