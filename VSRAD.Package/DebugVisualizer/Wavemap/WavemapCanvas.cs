using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VSRAD.Package.DebugVisualizer.Wavemap;

namespace VSRAD.Package.DebugVisualizer
{
    class WavemapCanvas
    {
        private readonly Canvas _canvas;
        private readonly List<List<Rectangle>> _rectangles = new List<List<Rectangle>> { new List<Rectangle>(), new List<Rectangle>() };
        private WavemapView _view;

        public int Height => _view.WavesPerGroup * 6 + 2; // 6 is rectangle width, +2 for borders
        public int Width => _view.GroupCount * 6 + 2;

        public WavemapCanvas(Canvas canvas)
        {
            _canvas = canvas;
        }

        public void SetData(WavemapView view)
        {
            _view = view;

            if (view.WavesPerGroup != _rectangles.Count)
            {
                if (view.WavesPerGroup > _rectangles.Count)
                {
                    var rCount = _rectangles.Count;
                    for (int i = rCount; i < view.WavesPerGroup; ++i)
                    {
                        _rectangles.Add(new List<Rectangle>());
                    }
                }
                else
                {
                    for (int i = view.WavesPerGroup; i < _rectangles.Count; ++i)
                        for (int j = 0; j < view.GroupCount; ++j)
                            _rectangles[i][j].Visibility = System.Windows.Visibility.Hidden;
                }
            }

            if (_rectangles[0].Count != view.GroupCount)
            {
                if (view.GroupCount > _rectangles[0].Count)
                {
                    for (int i = _rectangles[0].Count; i < view.GroupCount; ++i)
                    {
                        for (int j = 0; j < _rectangles.Count; ++j)
                        {
                            var r = InitiateWaveRectangle(j, i);
                            _rectangles[j].Add(r);
                            _canvas.Children.Add(r);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < view.WavesPerGroup; ++i)
                        for (int j = view.GroupCount; j < _rectangles[0].Count; ++j)
                            _rectangles[i][j].Visibility = System.Windows.Visibility.Hidden;
                }
            }

            for (int i = 0; i < view.GroupCount; ++i)
                for (int j = 0; j < view.WavesPerGroup; ++j)
                    UpdateWaveRectangle(j, i);
            _canvas.InvalidateVisual();
        }

        private static Rectangle InitiateWaveRectangle(int row, int column)
        {
            var r = new Rectangle();
            r.Visibility = System.Windows.Visibility.Hidden;
            r.Height = 7;
            r.Width = 7;
            r.StrokeThickness = 1;
            r.Stroke = Brushes.Black;
            Canvas.SetLeft(r, 1 + 6 * column);
            Canvas.SetTop(r, 1 + 6 * row);
            return r;
        }

        private void UpdateWaveRectangle(int row, int column)
        {
            var r = _rectangles[row][column];
            var validWave = _view != null && _view.IsValidWave(row, column);
            r.ToolTip = new ToolTip()
            {
                Content = validWave ?
                    $"Group: {_view[row, column].GroupIdx}\nWave: {_view[row, column].WaveIdx}\nLine: {_view[row, column].BreakLine}"
                    : ""
            };

            r.Fill = validWave ? _view[row, column].BreakColor : Brushes.Gray;
            r.Visibility = validWave ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
        }
    }
}
