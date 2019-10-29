using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms.Integration;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class VisualizerTableHost : WindowsFormsHost
    {
        public void Setup(VisualizerTable table)
        {
            Child = table;
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                var scaleFactor = graphics.DpiY / 96;
                table.ScaleControls(scaleFactor);
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            (Child as VisualizerTable)?.ScaleControls((float)newDpi.DpiScaleY);
            base.OnDpiChanged(oldDpi, newDpi);
        }
    }
}
