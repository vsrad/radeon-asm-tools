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
        private static extern int ColorHLSToRGB(ushort h, ushort l, ushort s);

        [DllImport("shlwapi.dll")]
        private static extern void ColorRGBToHLS(int win32rgb, ref ushort h, ref ushort l, ref ushort s);

        public static void ToHls(this Color c, ref ushort h, ref ushort l, ref ushort s) =>
            ColorRGBToHLS(ColorTranslator.ToWin32(c), ref h, ref l, ref s);

        public static Color FromHls(ushort h, ushort l, ushort s) =>
            ColorTranslator.FromWin32(ColorHLSToRGB(h, l, s));

        public static Color ScaleLightness(this Color c, float k)
        {
            ushort h = 0, l = 0, s = 0;
            c.ToHls(ref h, ref l, ref s);
            l = (ushort)(l * k);
            return FromHls(h, l, s);
        }
    }
}
