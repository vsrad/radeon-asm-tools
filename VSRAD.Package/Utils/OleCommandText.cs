using Microsoft.VisualStudio.OLE.Interop;
using System;
using System.Runtime.InteropServices;

namespace VSRAD.Package.Utils
{
    public static class OleCommandText
    {
        // See annotated version at https://github.com/microsoft/PTVS/blob/1d04f01b7b902a9e1051b4080770b4a27e6e97e7/Common/Product/SharedProject/Misc/NativeMethods.cs

        public static OLECMDTEXTF GetFlags(IntPtr pCmdTextInt)
        {
            if (pCmdTextInt == IntPtr.Zero)
                return OLECMDTEXTF.OLECMDTEXTF_NONE;

            var pCmdText = (OLECMDTEXT)Marshal.PtrToStructure(pCmdTextInt, typeof(OLECMDTEXT));

            if ((pCmdText.cmdtextf & (int)OLECMDTEXTF.OLECMDTEXTF_NAME) != 0)
                return OLECMDTEXTF.OLECMDTEXTF_NAME;

            if ((pCmdText.cmdtextf & (int)OLECMDTEXTF.OLECMDTEXTF_STATUS) != 0)
                return OLECMDTEXTF.OLECMDTEXTF_STATUS;

            return OLECMDTEXTF.OLECMDTEXTF_NONE;
        }

        public static void SetText(IntPtr pCmdTextInt, string text)
        {
            var pCmdText = (OLECMDTEXT)Marshal.PtrToStructure(pCmdTextInt, typeof(OLECMDTEXT));
            char[] menuText = text.ToCharArray();

            var offset = Marshal.OffsetOf(typeof(OLECMDTEXT), "rgwz");
            var offsetToCwActual = Marshal.OffsetOf(typeof(OLECMDTEXT), "cwActual");

            int maxChars = Math.Min((int)pCmdText.cwBuf - 1, menuText.Length);

            Marshal.Copy(menuText, 0, (IntPtr)((long)pCmdTextInt + (long)offset), maxChars);
            Marshal.WriteInt16((IntPtr)((long)pCmdTextInt + (long)offset + maxChars * 2), 0);
            Marshal.WriteInt32((IntPtr)((long)pCmdTextInt + (long)offsetToCwActual), maxChars + 1);
        }
    }
}
