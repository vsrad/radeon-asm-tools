using System;
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
        private static extern int ColorHLSToRGB(int h, int l, int s);

        [DllImport("shlwapi.dll")]
        private static extern void ColorRGBToHLS(int win32rgb, ref int h, ref int l, ref int s);

        public static void ToHls(this Color c, ref int h, ref int l, ref int s) =>
            ColorRGBToHLS(ColorTranslator.ToWin32(c), ref h, ref l, ref s);

        public static Color FromHls(int h, int l, int s) =>
            ColorTranslator.FromWin32(ColorHLSToRGB(h, l, s));
    }
}
