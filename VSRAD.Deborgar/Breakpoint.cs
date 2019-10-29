using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace VSRAD.Deborgar
{
    public sealed class Breakpoint : IDebugPendingBreakpoint2, IDebugBoundBreakpoint2
    {
        private readonly BreakpointManager _manager;
        private readonly IDebugBreakpointRequest2 _request;

        public BreakpointResolution Resolution { get; }

        private bool _enabled = false;

        public Breakpoint(BreakpointManager manager, IDebugBreakpointRequest2 request, BreakpointResolution resolution)
        {
            _manager = manager;
            _request = request;
            Resolution = resolution;
        }

        public int Enable(int fEnable)
        {
            var newState = fEnable == 0 ? false : true;
            if (newState != _enabled)
            {
                _enabled = newState;
                if (_enabled == true)
                {
                    _manager.AddBreakpoint(this);
                }
                else
                {
                    _manager.RemoveBreakpoint(this);
                }
            }
            return VSConstants.S_OK;
        }

        public int Delete()
        {
            _manager.RemoveBreakpoint(this);
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
            ppBPResolution = Resolution;
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