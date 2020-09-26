using System;
using System.Collections.Generic;
using System.Windows.Shapes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace VSRAD.Package.DebugVisualizer
{
    class WavemapCanvas
    {
        private readonly Canvas _canvas;
        private readonly Rectangle[][] _rectangles = { new Rectangle[200], new Rectangle[200] };

        public WavemapCanvas(Canvas canvas)
        {
            _canvas = canvas;
            for (int y = 0; y < 2; ++y)
            {
                for (int i = 0, x = 1; i < 200; ++i, x += 6)
                {
                    var r = new Rectangle();
                    r.Fill = Brushes.Gray;
                    r.Height = 7;
                    r.Width = 7;
                    r.StrokeThickness = 1;
                    r.Stroke = Brushes.Black;
                    Canvas.SetLeft(r, x);
                    Canvas.SetTop(r, 7 * y);
                    _rectangles[y][i] = r;
                    _canvas.Children.Add(r);
                }
            }
        }
    }
}
