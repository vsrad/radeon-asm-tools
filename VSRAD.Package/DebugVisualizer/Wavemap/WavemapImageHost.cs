using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    class WavemapImageHost : WindowsFormsHost
    {
        public void Setup()
        {
            var box = new PictureBox();
            box.SizeMode = PictureBoxSizeMode.StretchImage;
            Child = box;
        }
    }
}
