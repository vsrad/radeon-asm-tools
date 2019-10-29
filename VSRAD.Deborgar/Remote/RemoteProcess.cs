using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace VSRAD.Deborgar.Remote
{
    sealed class RemoteProcess : IDebugProcess2
    {
        private readonly Guid _id = Guid.NewGuid();
        private readonly AD_PROCESS_ID _adProcessId;
        private readonly RemotePort _port;

        public IDebugProgram2 Program { get; }

        public RemoteProcess(RemotePort port)
        {
            _port = port;
            _adProcessId = new AD_PROCESS_ID
            {
                ProcessIdType = (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_GUID,
                guidProcessId = _id
            };
            Program = new Program(this);
        }

        int IDebugProcess2.GetInfo(enum_PROCESS_INFO_FIELDS fields, PROCESS_INFO[] pProcessInfo)
        {
            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME) != 0)
            {
                pProcessInfo[0].bstrBaseName = Constants.RemotePortName;
                pProcessInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME;
            }
            if ((fields & enum_PROCESS_INFO_FIELDS.PIF_ATTACHED_SESSION_NAME) != 0)
            {
                pProcessInfo[0].bstrAttachedSessionName = Constants.RemotePortName;
                pProcessInfo[0].Fields |= enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME;
            }

            return VSConstants.S_OK;
        }

        int IDebugProcess2.EnumPrograms(out IEnumDebugPrograms2 ppEnum)
        {
            ppEnum = new AD7ProgramEnum(new[] { Program });
            return VSConstants.S_OK;
        }

        int IDebugProcess2.GetName(enum_GETNAME_TYPE gnType, out string pbstrName)
        {
            pbstrName = Constants.RemotePortName;
            return VSConstants.S_OK;
        }

        int IDebugProcess2.GetPhysicalProcessId(AD_PROCESS_ID[] pProcessId)
        {
            pProcessId[0] = _adProcessId;
            return VSConstants.S_OK;
        }

        int IDebugProcess2.GetProcessId(out Guid pguidProcessId)
        {
            pguidProcessId = _id;
            return VSConstants.S_OK;
        }

        int IDebugProcess2.GetPort(out IDebugPort2 ppPort)
        {
            ppPort = _port;
            return VSConstants.S_OK;
        }

        int IDebugProcess2.Terminate() => VSConstants.S_OK;

        #region Not implemented

        int IDebugProcess2.GetServer(out IDebugCoreServer2 ppServer)
        {
            throw new NotImplementedException();
        }

        int IDebugProcess2.Attach(IDebugEventCallback2 pCallback, Guid[] rgguidSpecificEngines, uint celtSpecificEngines, int[] rghrEngineAttach)
        {
            throw new NotImplementedException();
        }

        int IDebugProcess2.CanDetach()
        {
            throw new NotImplementedException();
        }

        int IDebugProcess2.Detach()
        {
            throw new NotImplementedException();
        }

        int IDebugProcess2.GetAttachedSessionName(out string pbstrSessionName)
        {
            throw new NotImplementedException();
        }

        int IDebugProcess2.EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            throw new NotImplementedException();
        }

        int IDebugProcess2.CauseBreak()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
