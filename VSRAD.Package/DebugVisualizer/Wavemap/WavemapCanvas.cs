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

        public int Height => _view.WavesPerGroup * (_rectangleSize - 1) + 2;
        public int Width => _view.GroupCount * (_rectangleSize - 1) + 2;

        private int _rectangleSize;
        public int RectangleSize
        {
            get => _rectangleSize;
            set => SetRectangleSize(value);
        }

        public WavemapCanvas(Canvas canvas, int rectangleSize)
        {
            _canvas = canvas;
            _rectangleSize = rectangleSize;
        }

        private void SetRectangleSize(int size)
        {
            _rectangleSize = size;
            for (int i = 0; i < _rectangles.Count; ++i)
            {
                for (int j = 0; j < _rectangles[i].Count; ++j)
                {
                    _rectangles[i][j].Height = size;
                    _rectangles[i][j].Width = size;
                    Canvas.SetLeft(_rectangles[i][j], 1 + (size - 1) * j);
                    Canvas.SetTop(_rectangles[i][j], 1 + (size - 1) * i);
                }
            }
            _canvas.InvalidateVisual();
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
                        for (int j = 0; j < _rectangles[i].Count; ++j)
                            _rectangles[i][j].Visibility = System.Windows.Visibility.Hidden;
                }
            }

            for (int j = 0; j < _rectangles.Count; ++j)
            {
                if (_rectangles[j].Count != view.GroupCount)
                {
                    if (view.GroupCount > _rectangles[j].Count)
                    {
                        for (int i = _rectangles[j].Count; i < view.GroupCount; ++i)
                        {
                            var r = InitiateWaveRectangle(j, i);
                            _rectangles[j].Add(r);
                            _canvas.Children.Add(r);
                        }
                    }
                    else
                    {
                        for (int i = view.GroupCount; i < _rectangles[j].Count; ++i)
                            _rectangles[j][i].Visibility = System.Windows.Visibility.Hidden;
                    }
                }
            }

            for (int i = 0; i < view.GroupCount; ++i)
                for (int j = 0; j < view.WavesPerGroup; ++j)
                    UpdateWaveRectangle(j, i);
            _canvas.InvalidateVisual();
        }

        private Rectangle InitiateWaveRectangle(int row, int column)
        {
            var r = new Rectangle();
            r.Visibility = System.Windows.Visibility.Hidden;
            r.Height = _rectangleSize;
            r.Width = _rectangleSize;
            r.StrokeThickness = 1;
            r.Stroke = Brushes.Black;
            Canvas.SetLeft(r, 1 + (_rectangleSize - 1) * column);
            Canvas.SetTop(r, 1 + (_rectangleSize - 1) * row);
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
