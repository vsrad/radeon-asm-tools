using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Collections.Generic;

namespace VSRAD.Deborgar
{
    public sealed class DebugThread : IDebugThread2, IDebugThread100
    {
        private readonly DebugProgram _program;
        private readonly uint _threadId;
        private readonly List<DebugThreadFrame> _frames;

        public DebugThread(DebugProgram program, uint threadId, IReadOnlyList<(string Name, string SourcePath, uint SourceLine)> callStack)
        {
            _program = program;
            _threadId = threadId;
            _frames = new List<DebugThreadFrame>();
            foreach (var frame in callStack)
                _frames.Add(new DebugThreadFrame(frame.Name, frame.SourcePath, new TEXT_POSITION { dwColumn = 0, dwLine = frame.SourceLine }));
        }

        public int EnumFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, out IEnumDebugFrameInfo2 ppEnum)
        {
            var frameInfo = new FRAMEINFO[_frames.Count];
            for (var i = 0; i < _frames.Count; ++i)
                _frames[i].SetFrameInfo(dwFieldSpec, out frameInfo[i]);
            ppEnum = new AD7FrameInfoEnum(frameInfo);
            return VSConstants.S_OK;
        }

        public int GetName(out string pbstrName)
        {
            pbstrName = _frames.Count > 0 ? _frames[0].DisplayName : "";
            return VSConstants.S_OK;
        }

        public int GetThreadId(out uint pdwThreadId)
        {
            pdwThreadId = _threadId;
            return VSConstants.S_OK;
        }

        public int GetThreadProperties100(uint dwFields, THREADPROPERTIES100[] ptp)
        {
            var fields = (enum_THREADPROPERTY_FIELDS100)dwFields;
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_ID) != 0)
            {
                ((IDebugThread2)this).GetThreadId(out ptp[0].dwThreadId);
                ptp[0].dwManagedThreadId = ptp[0].dwThreadId;
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_ID;
            }
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_DISPLAY_NAME) != 0)
            {
                ((IDebugThread100)this).GetThreadDisplayName(out ptp[0].bstrDisplayName);
                ptp[0].DisplayNamePriority = 1;
                ptp[0].dwFields |= (uint)(enum_THREADPROPERTY_FIELDS100.TPF100_DISPLAY_NAME | enum_THREADPROPERTY_FIELDS100.TPF100_DISPLAY_NAME_PRIORITY);
            }
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_LOCATION) != 0)
            {
                ptp[0].bstrLocation = _frames.Count > 0 ? _frames[0].Location : "";
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_LOCATION;
            }
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_STATE) != 0)
            {
                ptp[0].dwThreadState = (int)enum_THREADSTATE.THREADSTATE_DEAD;
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_STATE;
            }
            if ((fields & enum_THREADPROPERTY_FIELDS100.TPF100_STATE) != 0)
            {
                ptp[0].dwThreadState = (int)enum_THREADSTATE.THREADSTATE_DEAD;
                ptp[0].dwFields |= (uint)enum_THREADPROPERTY_FIELDS100.TPF100_STATE;
            }
            return VSConstants.S_OK;
        }

        #region IDebugThread2 Members

        int IDebugThread2.SetThreadName(string pszName)
        {
            return VSConstants.E_NOTIMPL;
        }

        int IDebugThread2.GetProgram(out IDebugProgram2 ppProgram)
        {
            ppProgram = _program;
            return VSConstants.S_OK;
        }

        int IDebugThread2.CanSetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            return VSConstants.S_FALSE;
        }

        int IDebugThread2.SetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext)
        {
            return VSConstants.E_NOTIMPL;
        }

        int IDebugThread2.Suspend(out uint pdwSuspendCount)
        {
            pdwSuspendCount = 0;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugThread2.Resume(out uint pdwSuspendCount)
        {
            pdwSuspendCount = 0;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugThread2.GetThreadProperties(enum_THREADPROPERTY_FIELDS dwFields, THREADPROPERTIES[] ptp)
        {
            return VSConstants.E_NOTIMPL; // See GetThreadProperties100
        }

        int IDebugThread2.GetLogicalThread(IDebugStackFrame2 pStackFrame, out IDebugLogicalThread2 ppLogicalThread)
        {
            ppLogicalThread = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion

        #region IDebugThread100 Members

        int IDebugThread100.GetFlags(out uint pFlags)
        {
            pFlags = 0;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugThread100.SetFlags(uint flags)
        {
            return VSConstants.E_NOTIMPL;
        }

        int IDebugThread100.CanDoFuncEval()
        {
            return VSConstants.S_FALSE;
        }

        int IDebugThread100.GetThreadDisplayName(out string bstrDisplayName)
        {
            return GetName(out bstrDisplayName);
        }

        int IDebugThread100.SetThreadDisplayName(string bstrDisplayName)
        {
            return VSConstants.E_NOTIMPL;
        }

        #endregion
    }
}
