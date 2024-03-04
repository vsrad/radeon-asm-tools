using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace VSRAD.Deborgar
{
    public sealed class DebugProgram : IDebugProgramNode2, IDebugProgram2
    {
        private readonly Guid _programId = Guid.NewGuid();
        private readonly List<DebugThread> _breakThreads = new List<DebugThread>();

        private readonly IDebugProcess2 _ad7Process;
        private IDebugEngine2 _ad7Engine;
        private IDebugEventCallback2 _ad7Callback;

        private IEngineIntegration _engineIntegration;

        public DebugProgram(IDebugProcess2 process)
        {
            _ad7Process = process;
        }

        public void AttachDebugger(IDebugEngine2 ad7Engine, IDebugEventCallback2 ad7Callback, IEngineIntegration engineIntegration)
        {
            _ad7Engine = ad7Engine;
            _ad7Callback = ad7Callback;
            _engineIntegration = engineIntegration;
            _engineIntegration.ExecutionCompleted += ExecutionCompleted;

            SendAD7Event(new AD7EngineCreateEvent(ad7Engine));
            SendAD7Event(new AD7ProgramCreateEvent());
            SendAD7Event(new AD7LoadCompleteEvent());
        }

        public void Execute(bool step)
        {
            ClearBreakLocations();
            _engineIntegration.Execute(step);
        }

        public int Terminate()
        {
            _ad7Process.Terminate();
            _engineIntegration.ExecutionCompleted -= ExecutionCompleted;
            ClearBreakLocations();
            SendAD7Event(new AD7ProgramDestroyEvent());
            return VSConstants.S_OK;
        }

        public int CauseBreak()
        {
            _engineIntegration.CauseBreak();
            return VSConstants.S_OK;
        }

        private void ExecutionCompleted(object sender, ExecutionCompletedEventArgs e)
        {
            foreach (var instance in e.BreakLocations)
            {
                var thread = new DebugThread(this, instance.LocationId, instance.CallStack);
                _breakThreads.Add(thread);
                SendAD7Event(new AD7ThreadCreateEvent(), thread);
            }
            // Must create all AD7 threads before sending break events
            foreach (var thread in _breakThreads)
            {
                if (e.IsStepping)
                    SendAD7Event(new AD7StepCompleteEvent(), thread);
                else
                    SendAD7Event(new AD7BreakCompleteEvent(), thread);
            }
        }

        private void SendAD7Event(AD7Event eventObject, IDebugThread2 ad7Thread = null)
        {
            ErrorHandler.ThrowOnFailure(eventObject.GetAttributes(out var attributes));
            ErrorHandler.ThrowOnFailure(_ad7Callback.Event(_ad7Engine, _ad7Process, this, ad7Thread, eventObject, eventObject.GUID, attributes));
        }

        private void ClearBreakLocations()
        {
            foreach (var oldInstance in _breakThreads)
                SendAD7Event(new AD7ThreadDestroyEvent(), oldInstance);
            _breakThreads.Clear();
        }

        public int GetEngineInfo(out string pbstrEngine, out Guid pguidEngine)
        {
            (pbstrEngine, pguidEngine) = (Constants.DebugEngineName, Constants.DebugEngineGuid);
            return VSConstants.S_OK;
        }

        #region IDebugProgram2 Members

        int IDebugProgram2.EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            var ad7Threads = new IDebugThread2[_breakThreads.Count];
            for (var i = 0; i < _breakThreads.Count; ++i)
                ad7Threads[i] = _breakThreads[i];
            ppEnum = new AD7ThreadEnum(ad7Threads);
            return VSConstants.S_OK;
        }

        int IDebugProgram2.Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT step)
        {
            switch (sk)
            {
                case enum_STEPKIND.STEP_INTO:
                case enum_STEPKIND.STEP_OUT:
                case enum_STEPKIND.STEP_OVER:
                    Execute(step: true);
                    return VSConstants.S_OK;
                default:
                    return VSConstants.E_NOTIMPL;
            }
        }

        int IDebugProgram2.Execute()
        {
            Execute(step: false);
            return VSConstants.S_OK;
        }

        int IDebugProgram2.Continue(IDebugThread2 thread)
        {
            Execute(step: false);
            return VSConstants.S_OK;
        }

        int IDebugProgram2.GetName(out string pbstrName)
        {
            pbstrName = Constants.ProgramName;
            return VSConstants.S_OK;
        }

        int IDebugProgram2.GetProgramId(out Guid pguidProgramId)
        {
            pguidProgramId = _programId;
            return VSConstants.S_OK;
        }

        int IDebugProgram2.GetProcess(out IDebugProcess2 process)
        {
            process = _ad7Process;
            return VSConstants.S_OK;
        }

        int IDebugProgram2.CanDetach()
        {
            return VSConstants.S_OK;
        }

        int IDebugProgram2.Detach()
        {
            return Terminate();
        }

        int IDebugProgram2.GetDebugProperty(out IDebugProperty2 ppProperty)
        {
            ppProperty = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgram2.EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum)
        {
            ppEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgram2.GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
        {
            ppMemoryBytes = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgram2.GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 pCodeContext, out IDebugDisassemblyStream2 ppDisassemblyStream)
        {
            ppDisassemblyStream = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgram2.EnumModules(out IEnumDebugModules2 ppEnum)
        {
            ppEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgram2.GetENCUpdate(out object ppUpdate)
        {
            ppUpdate = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgram2.EnumCodePaths(string pszHint, IDebugCodeContext2 pStart, IDebugStackFrame2 pFrame, int fSource, out IEnumCodePaths2 ppEnum, out IDebugCodeContext2 ppSafety)
        {
            (ppEnum, ppSafety) = (null, null);
            return VSConstants.E_NOTIMPL;
        }

        int IDebugProgram2.WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl)
        {
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugProgramNode2 Members

        int IDebugProgramNode2.GetHostPid(AD_PROCESS_ID[] pHostProcessId)
        {
            _ad7Process.GetPhysicalProcessId(pHostProcessId);
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

        #endregion
    }
}
