using System;
using System.Collections.Generic;
using System.Windows.Shapes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using VSRAD.Package.DebugVisualizer.Wavemap;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Debugger.Interop;
using VSRAD.Package.Server;

namespace VSRAD.Package.DebugVisualizer
{
    class WavemapCanvas
    {
        private readonly Canvas _canvas;
        private readonly Rectangle[][] _rectangles = { new Rectangle[200], new Rectangle[200] };
        private WavemapView _view;

        public WavemapCanvas(Canvas canvas)
        {
            _canvas = canvas;
            for (int i = 0; i < 200; ++i)
            {
                for (int j = 0; j < 2; ++j)
                {
                    var r = GetWaveRectangleByCoordinates(j, i);
                    _rectangles[j][i] = r;
                    _canvas.Children.Add(r);
                }
            }
        }

        public void SetData(WavemapView view)
        {
            _view = view;
            for (int i = 0; i < 200; ++i)
            {
                for (int j = 0; j < 2; ++j)
                {
                    var r = GetWaveRectangleByCoordinates(j, i);
                    _canvas.Children.OfType<Rectangle>().ElementAt(j * 200 + i).Fill = r.Fill;
                    _canvas.Children.OfType<Rectangle>().ElementAt(j * 200 + i).ToolTip = r.ToolTip;
                }
            }
            _canvas.InvalidateVisual();
        }

        private Rectangle GetWaveRectangleByCoordinates(int row, int column)
        {
            var validWave = _view != null && _view.IsValidWave(row, column);
            var r = new Rectangle();
            r.ToolTip = new ToolTip() { Content = validWave ?
                $"Group: {_view[row, column].GroupIdx}\nWave: {_view[row, column].WaveIdx}\nLine: {_view[row, column].BreakLine}"
                : "No data"
            };
            r.Fill = validWave ? _view[row, column].BreakColor : Brushes.Gray;
            r.Height = 7;
            r.Width = 7;
            r.StrokeThickness = 1;
            r.Stroke = Brushes.Black;
            Canvas.SetLeft(r, 1 + 6 * column);
            Canvas.SetTop(r, 1 + 6 * row);
            return r;
        }
    }
}
