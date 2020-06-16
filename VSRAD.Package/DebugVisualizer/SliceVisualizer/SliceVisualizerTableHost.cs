using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.Integration;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    class SliceVisualizerTableHost : WindowsFormsHost
    {
        public void Setup(SliceVisualizerTable table)
        {
            Child = table;
            /*
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                var scaleFactor = graphics.DpiY / 96;
                table.ScaleControls(scaleFactor);
            }
            */
        }
    }
}
