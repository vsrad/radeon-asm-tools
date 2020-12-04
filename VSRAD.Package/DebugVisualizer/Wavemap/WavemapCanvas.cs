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
        private readonly List<List<Rectangle>> _rectangles = new List<List<Rectangle>> { new List<Rectangle>(), new List<Rectangle>() };
        private WavemapView _view;
        private BitmapWrapper _bitmapWrapper;
        private Image _img;

        public int Height => (int)Math.Ceiling(_img.ActualHeight);
        public int Width => (int)Math.Ceiling(_img.ActualWidth);

        private int _rectangleSize;
        public int RectangleSize
        {
            get => _rectangleSize;
            set => SetRectangleSize(value);
        }

        public WavemapCanvas(Image image, int rectangleSize)
        {
            _img = image;
            _rectangleSize = 7; // const for now
            _bitmapWrapper = new BitmapWrapper();
        }

        private void SetRectangleSize(int size)
        {
            _rectangleSize = size;
        }

        public void SetData(WavemapView view)
        {
            if (view.WavesPerGroup == 0)
            {
                _img.Source = null;
                return;
            }

            _view = view;
            _img.Source = _bitmapWrapper.GetImageFromWavemapView(view);
        }
    }
}
