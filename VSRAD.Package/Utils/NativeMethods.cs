using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VSRAD.Package.Utils
{
    public static class NativeMethods
    {
        /* https://stackoverflow.com/a/487757 */
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, Int32 wMsg, bool wParam, Int32 lParam);
        private const int WM_SETREDRAW = 11;

        public static void SuspendDrawing(this Control control)
        {
            _ = SendMessage(control.Handle, WM_SETREDRAW, false, 0);
        }

        public static void ResumeDrawing(this Control control)
        {
            _ = SendMessage(control.Handle, WM_SETREDRAW, true, 0);
            control.Refresh();
        }
    }
}
