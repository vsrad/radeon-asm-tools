using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VSRAD.Package.Utils
{
    public static class NativeMethods
    {
        /* https://stackoverflow.com/a/487757 */
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint wMsg, UIntPtr wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 11;

        public const int MK_SHIFT = 0x4;
        public const int MK_CONTROL = 0x8;

        public static void SuspendDrawing(this Control control)
        {
            _ = SendMessage(control.Handle, WM_SETREDRAW, UIntPtr.Zero, IntPtr.Zero);
        }

        public static void ResumeDrawing(this Control control)
        {
            _ = SendMessage(control.Handle, WM_SETREDRAW, new UIntPtr(1), IntPtr.Zero);
            control.Refresh();
        }

        [DllImport("shlwapi.dll")]
        private static extern int ColorHLSToRGB(ushort h, ushort l, ushort s);

        [DllImport("shlwapi.dll")]
        private static extern void ColorRGBToHLS(int win32rgb, ref ushort h, ref ushort l, ref ushort s);

        public static void ToHls(this Color c, ref ushort h, ref ushort l, ref ushort s) =>
            ColorRGBToHLS(ColorTranslator.ToWin32(c), ref h, ref l, ref s);

        public static Color FromHls(ushort h, ushort l, ushort s) =>
            ColorTranslator.FromWin32(ColorHLSToRGB(h, l, s));

        #region GetParentProcessId
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessBasicInformation
        {
            public IntPtr Reserved1;
            public IntPtr PebAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);

        public static int GetParentProcessId(this Process childProcess)
        {
            var pbi = new ProcessBasicInformation();
            int status = NtQueryInformationProcess(childProcess.Handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
            if (status != 0)
                throw new Win32Exception(status);

            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }
        #endregion
    }
}
