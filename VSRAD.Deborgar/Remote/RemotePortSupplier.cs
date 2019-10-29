using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Runtime.InteropServices;

namespace VSRAD.Deborgar.Remote
{
    [ComVisible(true)]
    [Guid(Constants.RemotePortSupplierId)]
    public sealed class RemotePortSupplier : IDebugPortSupplier2
    {
        int IDebugPortSupplier2.AddPort(IDebugPortRequest2 pRequest, out IDebugPort2 ppPort)
        {
            ppPort = new RemotePort(this);
            return VSConstants.S_OK;
        }

        int IDebugPortSupplier2.GetPortSupplierName(out string pbstrName)
        {
            pbstrName = Constants.RemotePortSupplierName;
            return VSConstants.S_OK;
        }

        int IDebugPortSupplier2.GetPortSupplierId(out Guid pguidPortSupplier)
        {
            pguidPortSupplier = Constants.RemotePortSupplierGuid;
            return VSConstants.S_OK;
        }

        int IDebugPortSupplier2.CanAddPort() => VSConstants.S_OK;

        int IDebugPortSupplier2.GetPort(ref Guid guidPort, out IDebugPort2 ppPort) =>
            throw new NotImplementedException();

        int IDebugPortSupplier2.RemovePort(IDebugPort2 pPort) =>
            throw new NotImplementedException();

        int IDebugPortSupplier2.EnumPorts(out IEnumDebugPorts2 ppEnum) =>
            throw new NotImplementedException();
    }
}