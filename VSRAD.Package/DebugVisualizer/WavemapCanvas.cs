using System;
using System.Collections.Generic;
using System.Windows.Shapes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using VSRAD.Package.DebugVisualizer.Wavemap;

namespace VSRAD.Package.DebugVisualizer
{
    class WavemapCanvas
    {
        private readonly Canvas _canvas;
        private readonly Rectangle[][] _rectangles = { new Rectangle[200], new Rectangle[200] };
        private readonly WavemapView _wiew;

        public WavemapCanvas(Canvas canvas)
        {
            _canvas = canvas;

            var _data = new uint[7200];
            for (uint i = 3, j = 313; i < 7200; i += 18, j += 313)
                _data[i] = j;

            _wiew = new WavemapView(_data, 6, 3, 12);

            for (int y = 0; y < 2; ++y)
            {
                for (int i = 0, x = 1; i < 200; ++i, x += 6)
                {
                    var r = new Rectangle();
                    r.Fill = _wiew[y, i].BreakColor;
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
