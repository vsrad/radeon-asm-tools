using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    /*
     * ----------- Bitmap header -----------
     * 0x42 0x4d            -- magic number
     * 0xS  0x00 0x00 0x00  -- size of file (54 bytes of header + X bytes of data)
     * 0x00 0x00            -- Unused
     * 0x00 0x00            -- Unused
     * 0x36 0x00 0x00 0x00  -- Data offset
     * 0x28 0x00 0x00 0x00  -- DIB header size
     * 0xW  0x00 0x00 0x00  -- Width in pixels
     * 0xH  0x00 0x00 0x00  -- Height in pixels
     * 0x01 0x00            -- Number of color planes
     * 0x20 0x00            -- Bits per pixel (24 for RGB)
     * 0x00 0x00 0x00 0x00  -- BI_RGB, no pixel array compression used
     * 0xDS 0x00 0x00 0x00  -- Data size (pixels * 8)
     * 0x13 0x0b 0x00 0x00  -- horizontal DPI
     * 0x13 0x0b 0x00 0x00  -- vertical DPI
     * 0x00 0x00 0x00 0x00  -- Number of colors in the palette
     * 0x00 0x00 0x00 0x00  -- 0 means all colors are important 
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
            0x36, 0x00, 0x00, 0x00,
            0x28, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, // VARIABLE: width in pixels. Offset 18
            0x00, 0x00, 0x00, 0x00, // VARIABLE: height in pixels. Offset 22
            0x01, 0x00,
            0x20, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, // VARIABLE: data size. Offset 34
            0x13, 0x0b, 0x00, 0x00,
            0x13, 0x0b, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        private int _rSize = 7;
        private WavemapView _view;
        private PictureBox _box;

        private int _offset = 0;
        public int Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                SetData(_view);
            }
        }

        private int _elementsWidthX = 100;
        private int _elementsWidthY = 8;


        public WavemapImage(PictureBox box)
        {
            _box = box;
        }

        public void SetData(WavemapView view)
        {
            if (view == null || view.WavesPerGroup == 0)
            {
                _box.Image = null;
                return;
            }

            _view = view;
            var pixelCount = _elementsWidthX * _elementsWidthY * (_rSize + 1) * (_rSize + 1);
            var byteCount = pixelCount * 4;
            var imageData = new byte[byteCount + 54];
            _header.CopyTo(imageData, 0);
            var fileSizeBytes = BitConverter.GetBytes(54 + byteCount);
            var widthBytes = BitConverter.GetBytes(_elementsWidthX * _rSize + 1);
            var heightBytes = BitConverter.GetBytes(_elementsWidthY * _rSize + 1);
            var dataSizeBytes = BitConverter.GetBytes(byteCount);
            for (int i = 0; i < 4; i++)
            {
                imageData[2 + i] = fileSizeBytes[i];
                imageData[18 + i] = widthBytes[i];
                imageData[22 + i] = heightBytes[i];
                imageData[34 + i] = dataSizeBytes[i];
            }
            var byteWidth = _elementsWidthX * _rSize * 4 + 4;   // +4 for right border

            for (int i = 0; i < byteCount - 3; i += 4)
            {
                int row = i / byteWidth;
                if ((row % _rSize) == 0 || row / _rSize >= _elementsWidthY) continue;
                int col = i % byteWidth;
                if ((col % _rSize) == 0) continue;

                var viewRow = _elementsWidthY - 1 - row / _rSize;
                var viewCol = (col / _rSize / 4) + _elementsWidthX * _offset;
                var waveInfo = view[viewRow, viewCol];

                var flatIdx = i + 54;   // header offset

                imageData[flatIdx+0] = waveInfo.BreakColor[0]; // B
                imageData[flatIdx+1] = waveInfo.BreakColor[1]; // G
                imageData[flatIdx+2] = waveInfo.BreakColor[2]; // R
                imageData[flatIdx+3] = waveInfo.BreakColor[3]; // Alpha
            }

            _box.Image = LoadImage(imageData);
            _box.Size = _box.Image.Size;
            _box.Refresh();
        }

        private static Bitmap LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();

            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(image));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }
    }
}
