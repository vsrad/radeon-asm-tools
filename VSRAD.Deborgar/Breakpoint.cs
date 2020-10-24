using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Runtime.InteropServices;

namespace VSRAD.Deborgar
{
    public sealed class Breakpoint : IDebugPendingBreakpoint2, IDebugBoundBreakpoint2, IDebugBreakpointResolution2
    {
        public SourceFileLineContext SourceContext => new SourceFileLineContext(_sourcePath, GetTextPosition());

        private readonly Program _program;
        private readonly IDebugBreakpointRequest2 _request;
        private readonly IDebugDocumentPosition2 _documentInfo;
        private readonly string _sourcePath;

        private bool _enabled = false;

        public Breakpoint(Program program, IDebugBreakpointRequest2 request, IDebugDocumentPosition2 documentInfo)
        {
            _program = program;
            _request = request;
            _documentInfo = documentInfo;

            ErrorHandler.ThrowOnFailure(_documentInfo.GetFileName(out _sourcePath));
        }

        public int Enable(int fEnable)
        {
            var newState = fEnable == 0 ? false : true;
            if (newState != _enabled)
            {
                _enabled = newState;
                if (_enabled == true)
                    _program.AddBreakpoint(this);
                else
                    _program.RemoveBreakpoint(this);
            }
            return VSConstants.S_OK;
        }

        public int Delete()
        {
            _program.RemoveBreakpoint(this);
            return VSConstants.S_OK;
        }

        private TEXT_POSITION GetTextPosition()
        {
            var startPosition = new TEXT_POSITION[1];
            var endPosition = new TEXT_POSITION[1];

            ErrorHandler.ThrowOnFailure(_documentInfo.GetRange(startPosition, endPosition));
            return startPosition[0];
        }

        int IDebugBreakpointResolution2.GetBreakpointType(enum_BP_TYPE[] pBPType)
        {
            pBPType[0] = enum_BP_TYPE.BPT_CODE;
            return VSConstants.S_OK;
        }

        int IDebugBreakpointResolution2.GetResolutionInfo(enum_BPRESI_FIELDS dwFields, BP_RESOLUTION_INFO[] pBPResolutionInfo)
        {
            if ((dwFields & enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION) != 0)
            {
                pBPResolutionInfo[0].dwFields |= enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION;
                pBPResolutionInfo[0].bpResLocation = new BP_RESOLUTION_LOCATION
                {
                    bpType = (uint)enum_BP_TYPE.BPT_CODE,
                    // Taken from the engine sample
                    unionmember1 = Marshal.GetComInterfaceForObject(SourceContext, typeof(IDebugCodeContext2))
                };
            }
            if ((dwFields & enum_BPRESI_FIELDS.BPRESI_PROGRAM) != 0)
            {
                pBPResolutionInfo[0].dwFields |= enum_BPRESI_FIELDS.BPRESI_PROGRAM;
                pBPResolutionInfo[0].pProgram = _program;
            }

            return VSConstants.S_OK;
        }

        int IDebugPendingBreakpoint2.CanBind(out IEnumDebugErrorBreakpoints2 ppErrorEnum)
        {
            ppErrorEnum = null;
            return VSConstants.S_OK;
        }

        int IDebugPendingBreakpoint2.GetState(PENDING_BP_STATE_INFO[] pState)
        {
            pState[0].state = _enabled ? enum_PENDING_BP_STATE.PBPS_ENABLED : enum_PENDING_BP_STATE.PBPS_DISABLED;
            return VSConstants.S_OK;
        }

        int IDebugBoundBreakpoint2.GetState(enum_BP_STATE[] pState)
        {
            pState[0] = _enabled ? enum_BP_STATE.BPS_ENABLED : enum_BP_STATE.BPS_DISABLED;
            return VSConstants.S_OK;
        }

        int IDebugPendingBreakpoint2.GetBreakpointRequest(out IDebugBreakpointRequest2 ppBPRequest)
        {
            ppBPRequest = _request;
            return VSConstants.S_OK;
        }

        int IDebugBoundBreakpoint2.GetPendingBreakpoint(out IDebugPendingBreakpoint2 ppPendingBreakpoint)
        {
            ppPendingBreakpoint = this;
            return VSConstants.S_OK;
        }

        int IDebugBoundBreakpoint2.GetBreakpointResolution(out IDebugBreakpointResolution2 ppBPResolution)
        {
            ppBPResolution = this;
            return VSConstants.S_OK;
        }

        int IDebugPendingBreakpoint2.EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 ppEnum)
        {
            ppEnum = new AD7BoundBreakpointsEnum(new IDebugBoundBreakpoint2[] { this });
            return VSConstants.S_OK;
        }

        int IDebugPendingBreakpoint2.EnumErrorBreakpoints(enum_BP_ERROR_TYPE bpErrorType, out IEnumDebugErrorBreakpoints2 ppEnum)
        {
            ppEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugBoundBreakpoint2.GetHitCount(out uint pdwHitCount)
        {
            pdwHitCount = 0;
            return VSConstants.E_NOTIMPL;
        }

        int IDebugBoundBreakpoint2.SetHitCount(uint dwHitCount) => VSConstants.E_NOTIMPL;

        public int SetCondition(BP_CONDITION bpCondition) => throw new NotImplementedException();

        public int SetPassCount(BP_PASSCOUNT bpPassCount) => throw new NotImplementedException();

        int IDebugPendingBreakpoint2.Bind() => VSConstants.S_OK;

        int IDebugPendingBreakpoint2.Virtualize(int fVirtualize) => VSConstants.S_OK;
    }
}