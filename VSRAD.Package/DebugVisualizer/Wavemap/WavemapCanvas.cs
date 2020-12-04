using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VSRAD.Package.DebugVisualizer.Wavemap;

namespace VSRAD.Package.DebugVisualizer
{
    class WavemapCanvas
    {
        private readonly Canvas _canvas;
        private readonly List<List<Rectangle>> _rectangles = new List<List<Rectangle>> { new List<Rectangle>(), new List<Rectangle>() };
        private WavemapView _view;
        private BitmapWrapper _bitmapWrapper;

        public int Height => _view.WavesPerGroup == 0 ? 0 : _view.WavesPerGroup * (_rectangleSize - 1) + 2;
        public int Width => _view.WavesPerGroup == 0 ? 0 : _view.GroupCount * (_rectangleSize - 1) + 2;

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
            _bitmapWrapper = new BitmapWrapper();
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
            var img = new Image();
            img.Source = _bitmapWrapper.GetImageFromWavemapView(view);
            _canvas.Children.Clear();
            _canvas.Children.Add(img);
        }
    }
}
