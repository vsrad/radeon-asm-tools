using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;

namespace VSRAD.Deborgar
{
    public sealed class Program : IDebugProgramNode2, IDebugProgram3, IDebugThread2
    {
        private readonly IDebugProcess2 _process;
        private readonly Guid _programId = Guid.NewGuid();

        private readonly Dictionary<string, List<Breakpoint>> _breakpoints = new Dictionary<string, List<Breakpoint>>();

        private IEngineIntegration _engineIntegration;
        private IEngineCallbacks _callbacks;

        private StackFrame _breakFrame;

        public Program(IDebugProcess2 process)
        {
            _process = process;
        }

        public void AttachDebugger(IEngineIntegration engineIntegration, IEngineCallbacks callbacks)
        {
            _engineIntegration = engineIntegration;
            _engineIntegration.ExecutionCompleted += ExecutionCompleted;
            _callbacks = callbacks;
        }

        public int Terminate()
        {
            _process.Terminate();
            _engineIntegration.ExecutionCompleted -= ExecutionCompleted;
            return VSConstants.S_OK;
        }

        private void ExecutionCompleted(object sender, ExecutionCompletedEventArgs e)
        {
            var breakLines = e.Breakpoints.Select(br => br.BreakLine).ToArray();
            _breakFrame = new StackFrame(e.File, new SourceFileLineContext(e.File, breakLines));

            if (e.IsStepping)
                _callbacks.OnStepComplete();
            else
                _callbacks.OnBreakComplete();
        }

        public int Continue(IDebugThread2 thread) => ExecuteOnThread(thread);

        public int ExecuteOnThread(IDebugThread2 thread)
        {
            _engineIntegration.Execute(step: false);
            return VSConstants.S_OK;
        }

        public int Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT step)
        {
            switch (sk)
            {
                case enum_STEPKIND.STEP_INTO:
                case enum_STEPKIND.STEP_OUT:
                case enum_STEPKIND.STEP_OVER:
                    _engineIntegration.Execute(step: true);
                    return VSConstants.S_OK;
                default:
                    return VSConstants.E_NOTIMPL;
            }
        }

        public int CauseBreak()
        {
            _engineIntegration.CauseBreak();
            return VSConstants.S_OK;
        }

        public int EnumFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, out IEnumDebugFrameInfo2 ppEnum)
        {
            _breakFrame.SetFrameInfo(dwFieldSpec, out var frameInfo);
            ppEnum = new AD7FrameInfoEnum(new FRAMEINFO[] { frameInfo });
            return VSConstants.S_OK;
        }

        public int CreatePendingBreakpoint(IDebugBreakpointRequest2 request, out IDebugPendingBreakpoint2 breakpoint)
        {
            var requestInfo = new BP_REQUEST_INFO[1];
            ErrorHandler.ThrowOnFailure(request.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION, requestInfo));
            var bpLocation = requestInfo[0].bpLocation;
            if (bpLocation.bpLocationType != (uint)enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE)
            {
                breakpoint = null;
                return VSConstants.E_FAIL;
            }

            var documentInfo = (IDebugDocumentPosition2)Marshal.GetObjectForIUnknown(bpLocation.unionmember2);
            breakpoint = new Breakpoint(this, request, documentInfo);
            return VSConstants.S_OK;
        }

        public void AddBreakpoint(Breakpoint breakpoint)
        {
            var fileState = GetFileBreakpoints(breakpoint.SourceContext.SourcePath);
            fileState.Add(breakpoint);

            _callbacks.OnBreakpointBound(breakpoint);
        }

        public void RemoveBreakpoint(Breakpoint breakpoint)
        {
            var fileState = GetFileBreakpoints(breakpoint.SourceContext.SourcePath);
            fileState.Remove(breakpoint);
        }

        private List<Breakpoint> GetFileBreakpoints(string file)
        {
            if (!_breakpoints.TryGetValue(file, out var breakpoints))
            {
                breakpoints = new List<Breakpoint>();
                _breakpoints.Add(file, breakpoints);
            }
            return breakpoints;
        }

        #region IDebugProgram2/IDebugProgram3 Members

        public int EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            ppEnum = new AD7ThreadEnum(new IDebugThread2[] { this });
            return VSConstants.S_OK;
        }

        public int GetName(out string pbstrName)
        {
            pbstrName = Constants.ProgramName;
            return VSConstants.S_OK;
        }

        public int GetProgramId(out Guid pguidProgramId)
        {
            pguidProgramId = _programId;
            return VSConstants.S_OK;
        }

        public int GetProcess(out IDebugProcess2 process)
        {
            process = _process;
            return VSConstants.S_OK;
        }

        public int GetEngineInfo(out string pbstrEngine, out Guid pguidEngine)
        {
            pbstrEngine = Constants.DebugEngineName;
            pguidEngine = Constants.DebugEngineGuid;
            return VSConstants.S_OK;
        }

        public int CanDetach()
        {
            return VSConstants.S_OK;
        }

        public int Detach()
        {
            Terminate();
            _callbacks.OnProgramTerminated();
            return VSConstants.S_OK;
        }

        public int GetDebugProperty(out IDebugProperty2 ppProperty)
        {
            ppProperty = null;
            return VSConstants.E_NOTIMPL;
        }

        public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum)
        {
            ppEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            ppMemoryBytes = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 pCodeContext, out IDebugDisassemblyStream2 ppDisassemblyStream)
        {
            ppDisassemblyStream = null;
            return VSConstants.E_NOTIMPL;
        }

        public int EnumModules(out IEnumDebugModules2 ppEnum)
        {
            ppEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int GetENCUpdate(out object ppUpdate)
        {
            ppUpdate = null;
            return VSConstants.E_NOTIMPL;
        }

        public int EnumCodePaths(string pszHint, IDebugCodeContext2 pStart, IDebugStackFrame2 pFrame, int fSource, out IEnumCodePaths2 ppEnum, out IDebugCodeContext2 ppSafety)
        {
            ppEnum = null;
            ppSafety = null;
            return VSConstants.E_NOTIMPL;
        }

        public int WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl)
        {
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugProgramNode2 Members

        int IDebugProgramNode2.GetHostPid(AD_PROCESS_ID[] pHostProcessId)
        {
            _process.GetPhysicalProcessId(pHostProcessId);
            return VSConstants.S_OK;
        }

        int IDebugProgramNode2.GetHostName(enum_GETHOSTNAME_TYPE dwHostNameType, out string processName)
        {
            // We are using default transport and don't want to customize the process name
            processName = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgramNode2.GetProgramName(out string programName)
        {
            // We are using default transport and don't want to customize the process name
            programName = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugThread2 Members

        int IDebugThread2.GetName(out string pbstrName)
        {
            pbstrName = Constants.ThreadName;
            return VSConstants.S_OK;
        }

        int IDebugThread2.SetThreadName(string pszName)
        {
            return VSConstants.E_NOTIMPL;
        }

        int IDebugThread2.GetProgram(out IDebugProgram2 ppProgram)
        {
            ppProgram = this;
            return VSConstants.S_OK;
        }

        int IDebugThread2.CanSetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            return VSConstants.S_FALSE;
        }

        int IDebugThread2.SetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            throw new NotImplementedException();
        }

        int IDebugThread2.GetThreadId(out uint pdwThreadId)
        {
            pdwThreadId = 0;
            return VSConstants.S_OK;
        }

        int IDebugThread2.Suspend(out uint pdwSuspendCount)
        {
            throw new NotImplementedException();
        }

        int IDebugThread2.Resume(out uint pdwSuspendCount)
        {
            throw new NotImplementedException();
        }

        int IDebugThread2.GetThreadProperties(enum_THREADPROPERTY_FIELDS dwFields, THREADPROPERTIES[] ptp)
        {
            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_ID) != 0)
            {
                ptp[0].dwThreadId = 0;
                ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_ID;
            }
            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_NAME) != 0)
            {
                ptp[0].bstrName = Constants.ThreadName;
                ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_NAME;
            }
            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_STATE) != 0)
            {
                ptp[0].dwThreadState = (int)enum_THREADSTATE.THREADSTATE_STOPPED;
                ptp[0].dwFields |= enum_THREADPROPERTY_FIELDS.TPF_STATE;
            }

            return VSConstants.S_OK;
        }

        int IDebugThread2.GetLogicalThread(IDebugStackFrame2 pStackFrame, out IDebugLogicalThread2 ppLogicalThread)
        {
            ppLogicalThread = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region Deprecated interface methods

        int IDebugProgramNode2.Attach_V7(IDebugProgram2 pMDMProgram, IDebugEventCallback2 pCallback, uint dwReason)
        {
            Debug.Fail("This function is not called by the debugger");
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgramNode2.DetachDebugger_V7()
        {
            Debug.Fail("This function is not called by the debugger");
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgramNode2.GetHostMachineName_V7(out string hostMachineName)
        {
            Debug.Fail("This function is not called by the debugger");
            hostMachineName = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Attach(IDebugEventCallback2 pCallback)
        {
            Debug.Fail("This function is not called by the debugger");
            return VSConstants.E_NOTIMPL;
        }

        public int Execute()
        {
            Debug.Fail("This function is not called by the debugger.");
            return VSConstants.E_NOTIMPL;
        }

        #endregion
    }
}
