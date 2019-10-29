using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;

namespace VSRAD.Deborgar.Remote
{
    sealed class RemotePort : IDebugPort2
    {
        private readonly Guid _portId = Guid.NewGuid();
        private readonly RemotePortSupplier _portSupplier;

        public RemotePort(RemotePortSupplier portSupplier)
        {
            _portSupplier = portSupplier;
        }

        int IDebugPort2.GetPortName(out string pbstrName)
        {
            pbstrName = Constants.RemotePortName;
            return VSConstants.S_OK;
        }

        int IDebugPort2.GetPortId(out Guid pguidPort)
        {
            pguidPort = _portId;
            return VSConstants.S_OK;
        }

        int IDebugPort2.GetPortSupplier(out IDebugPortSupplier2 ppSupplier)
        {
            ppSupplier = _portSupplier;
            return VSConstants.S_OK;
        }

        int IDebugPort2.EnumProcesses(out IEnumDebugProcesses2 ppEnum)
        {
            ppEnum = new AD7ProcessEnum(new[] { new RemoteProcess(this) });
            return VSConstants.S_OK;
        }

        int IDebugPort2.GetPortRequest(out IDebugPortRequest2 ppRequest) =>
            throw new NotImplementedException();

        int IDebugPort2.GetProcess(AD_PROCESS_ID ProcessId, out IDebugProcess2 ppProcess) =>
            throw new NotImplementedException();
    }
}
