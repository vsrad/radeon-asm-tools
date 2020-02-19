using System;
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

        public static void SuspendDrawing(this Control control)
        {
            _ = SendMessage(control.Handle, WM_SETREDRAW, UIntPtr.Zero, IntPtr.Zero);
        }

        public static void ResumeDrawing(this Control control)
        {
            _ = SendMessage(control.Handle, WM_SETREDRAW, new UIntPtr(1), IntPtr.Zero);
            control.Refresh();
        }
    }
}
