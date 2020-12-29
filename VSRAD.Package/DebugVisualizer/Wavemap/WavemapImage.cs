using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    /*
     * ----------- Bitmap header -----------
     * 0x42 0x4d            -- magic number
     * 0xS  0x00 0x00 0x00  -- size of file (54 bytes of header + X bytes of data)
     * 0x00 0x00            -- Unused
     * 0x00 0x00            -- Unused
     * 0x7a 0x00 0x00 0x00  -- Data offset
     * 0x6c 0x00 0x00 0x00  -- DIB header size
     * 0xW  0x00 0x00 0x00  -- Width in pixels
     * 0xH  0x00 0x00 0x00  -- Height in pixels
     * 0x01 0x00            -- Number of color planes
     * 0x20 0x00            -- Bits per pixel (32 for RGBA)
     * 0x03 0x00 0x00 0x00  -- BI_BITFIELDS, no pixel array compression used
     * 0xDS 0x00 0x00 0x00  -- Data size (pixels * 8)
     * 0x13 0x0b 0x00 0x00  -- horizontal DPI
     * 0x13 0x0b 0x00 0x00  -- vertical DPI
     * 0x00 0x00 0x00 0x00  -- Number of colors in the palette
     * 0x00 0x00 0x00 0x00  -- 0 means all colors are important 
     * 0x00 0x00 0xff 0x00  -- Red channel bit mask
     * 0x00 0xff 0x00 0x00  -- Green channel bit mask
     * 0xff 0x00 0x00 0x00  -- Blue channel bit mask
     * 0x00 0x00 0x00 0xff  -- Alpha channel bit mask
     * 0x20 0x6E 0x69 0x57  -- LCS_WINDOWS_COLOR_SPACE
     * 24h* 00...00         -- CIEXYZTRIPLE Color Space endpoints
     * 0x00 0x00 0x00 0x00  -- Red Gamma
     * 0x00 0x00 0x00 0x00  -- Green Gamma
     * 0x00 0x00 0x00 0x00  -- Blue Gamma
     * ------------ DATA ------------
     */
    class WavemapImage
    {
        // initialize data with empty header
        private List<byte> _header = new List<byte>
        {
            0x42, 0x4d,
            0x36, 0x00, 0x00, 0x00, // VARIABLE: size of file, add data size. Offset 2
            0x00, 0x00,
            0x00, 0x00,
            0x7a, 0x00, 0x00, 0x00,
            0x6c, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, // VARIABLE: width in pixels. Offset 18
            0x00, 0x00, 0x00, 0x00, // VARIABLE: height in pixels. Offset 22
            0x01, 0x00,
            0x20, 0x00,
            0x03, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, // VARIABLE: data size. Offset 34
            0x13, 0x0b, 0x00, 0x00,
            0x13, 0x0b, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0xff, 0x00,
            0x00, 0xff, 0x00, 0x00,
            0xff, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0xff,
            0x20, 0x6E, 0x69, 0x57,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        private int _headerSize => _header.Count;
        private int _rSize = 7;
        private WavemapView _view;
        private Image _img;
        private VisualizerContext _context;

        private int _xOffset = 0;
        public int XOffset
        {
            get => _xOffset;
            set
            {
                _xOffset = value;
                SetData(_view);
            }
        }

        private int _yOffset = 0;
        public int YOffset
        {
            get => _yOffset;
            set
            {
                _yOffset = value;
                SetData(_view);
            }
        }

        public static int GridSizeX => 100;
        public static int GridSizeY => 8;


        public WavemapImage(Image image, VisualizerContext context)
        {
            _img = image;
            _context = context;
        }

        private void ShowWaveInfo(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var p = e.GetPosition(_img);
            var row = (int)(p.Y / _rSize) + GridSizeY * _yOffset;
            var col = (int)(p.X / _rSize) + GridSizeX * _xOffset;
            var waveInfo = _view[row, col];
            _context.CurrentWaveInfo = waveInfo.IsVisible
                ? $"G: {col}, W: {row}, L: {waveInfo.BreakLine}"
                : $"G: {col}, W: {row}, Out of range.";
        }

        public void SetData(WavemapView view)
        {
            if (view == null || view.WavesPerGroup == 0)
            {
                _img.Source = null;
                return;
            }

            _view = view;
            var pixelCount = GridSizeX * GridSizeY * (_rSize + 1) * (_rSize + 1);
            var byteCount = pixelCount * 4;
            var imageData = new byte[byteCount + _headerSize];
            _header.CopyTo(imageData, 0);
            var fileSizeBytes = BitConverter.GetBytes(_headerSize + byteCount);
            var widthBytes = BitConverter.GetBytes(GridSizeX * _rSize + 1);
            var heightBytes = BitConverter.GetBytes(GridSizeY * _rSize + 1);
            var dataSizeBytes = BitConverter.GetBytes(byteCount);
            for (int i = 0; i < 4; i++)
            {
                imageData[2 + i] = fileSizeBytes[i];
                imageData[18 + i] = widthBytes[i];
                imageData[22 + i] = heightBytes[i];
                imageData[34 + i] = dataSizeBytes[i];
            }
            var byteWidth = GridSizeX * _rSize * 4 + 4;   // +4 for left border
            var lastRow = GridSizeY * _rSize - 1;

            for (int i = 0; i < byteCount - 3; i += 4)
            {
                int row = i / byteWidth;
                int col = i % byteWidth;
                var flatIdx = i + _headerSize;   // header offset

                if (row / _rSize >= GridSizeY) continue;
                if ((row % _rSize) == 0 || (col % _rSize) == 0 || col == 0 || row == lastRow)
                {
                    imageData[flatIdx + 0] = 0; // B
                    imageData[flatIdx + 1] = 0; // G
                    imageData[flatIdx + 2] = 0; // R
                    imageData[flatIdx + 3] = 255; // Alpha
                    continue;
                }

                var viewRow = (GridSizeY - 1 - row / _rSize) + GridSizeY * _yOffset;
                var viewCol = (col / _rSize / 4) + GridSizeX * _xOffset;
                var waveInfo = view[viewRow, viewCol];

                for (int rwidth = 0; rwidth < _rSize - 1; ++rwidth)
                {
                    imageData[flatIdx + 0] = waveInfo.BreakColor.B; // B
                    imageData[flatIdx + 1] = waveInfo.BreakColor.G; // G
                    imageData[flatIdx + 2] = waveInfo.BreakColor.R; // R
                    imageData[flatIdx + 3] = waveInfo.BreakColor.A; // Alpha
                    flatIdx += 4;
                }

                i += (_rSize - 2) * 4;
            }

            _img.Source = LoadImage(imageData);

            _img.MouseMove -= ShowWaveInfo;
            _img.MouseMove += ShowWaveInfo;
        }

        private static BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;

            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(imageData))
            {

                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
            }
            return bitmap;
        }
    }
}
