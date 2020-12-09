using System;
using System.Windows.Controls;
using System.Windows.Forms;
using VSRAD.Package.DebugVisualizer.Wavemap;

namespace VSRAD.Package.DebugVisualizer
{
    class WavemapCanvas
    {
        private BitmapWrapper _bitmapWrapper;
        private PictureBox _box;

        public int Height => _box.Height;
        public int Width => _box.Width;

        private int _rectangleSize;
        public int RectangleSize
        {
            get => _rectangleSize;
            set => SetRectangleSize(value);
        }

        public WavemapCanvas(PictureBox box, int rectangleSize)
        {
            _box = box;
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
                _box.Image = null;
                return;
            }

            _box.Image = _bitmapWrapper.GetImageFromWavemapView(view);
            _box.Size = _box.Image.Size;
            _box.Refresh();
        }
    }
}
