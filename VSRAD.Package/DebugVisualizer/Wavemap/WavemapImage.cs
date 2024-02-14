using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

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
     * 0xc4 0x0e 0x00 0x00  -- horizontal DPM, must be set to 96 dpi
     * 0xc4 0x0e 0x00 0x00  -- vertical DPM, must be set to 96 dpi
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
    public sealed class WavemapImage
    {
        // initialize data with empty header
        private readonly List<byte> _header = new List<byte>
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
            0xc4, 0x0e, 0x00, 0x00,
            0xc4, 0x0e, 0x00, 0x00,
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
        private readonly Image _img;
        private readonly VisualizerContext _context;

        private int _gridSizeX;
        public int GridSizeX
        {
            get => _gridSizeX;
            private set { if (value >= 8) _gridSizeX = value; }
        }

        public int GridSizeY { get; private set; }

        private int _firstGroup;
        public int FirstGroup
        {
            get => _firstGroup;
            set { _firstGroup = value; DrawImage(); }
        }

        public sealed class NagivationEventArgs : EventArgs
        {
            public uint GroupIndex { get; set; }
            public uint? WaveIndex { get; set; }
            public BreakpointInfo Breakpoint { get; set; }
        }

        public event EventHandler<NagivationEventArgs> NavigationRequested;

        public event EventHandler Updated;

        public WavemapImage(Image image, VisualizerContext context)
        {
            _img = image;
            _img.MouseMove += ShowWaveInfo;
            _img.MouseRightButtonUp += ShowWaveMenu;

            _context = context;
            _context.PropertyChanged += VisualizerStateChanged;
            _context.Options.VisualizerOptions.PropertyChanged += VisualizerStateChanged;

            ((FrameworkElement)_img.Parent).SizeChanged += RecomputeGridSize;
        }

        private void VisualizerStateChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(VisualizerContext.Wavemap):
                case nameof(Options.VisualizerOptions.MaskLanes):
                case nameof(Options.VisualizerOptions.CheckMagicNumber):
                case nameof(Options.VisualizerOptions.MagicNumber):
                case nameof(Options.VisualizerOptions.WavemapElementSize):
                    DrawImage();
                    break;
            }
        }

        private void RecomputeGridSize(object sender, SizeChangedEventArgs e)
        {
            var rSize = _context.Options.VisualizerOptions.WavemapElementSize;
            var newGridSizeX = (int)((FrameworkElement)_img.Parent).ActualWidth / rSize;
            if (newGridSizeX != GridSizeX)
                DrawImage();
        }

        private void ShowWaveInfo(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (GetWaveAtImagePos(e.GetPosition(_img)) is WaveInfo wave)
                _context.WavemapSelection = wave;
        }

        private void ShowWaveMenu(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GetWaveAtImagePos(e.GetPosition(_img)) is WaveInfo wave)
            {
                var menu = new ContextMenu { PlacementTarget = _img };
                var goToGroup = new MenuItem { Header = $"Go to Group #{wave.GroupIndex}" };
                goToGroup.Click += (s, _) => NavigationRequested(this, new NagivationEventArgs { GroupIndex = wave.GroupIndex });
                menu.Items.Add(goToGroup);
                var goToWave = new MenuItem { Header = $"Go to Wave #{wave.WaveIndex} of Group #{wave.GroupIndex}" };
                goToWave.Click += (s, _) => NavigationRequested(this, new NagivationEventArgs { GroupIndex = wave.GroupIndex, WaveIndex = wave.WaveIndex });
                menu.Items.Add(goToWave);
                if (wave.Breakpoint is BreakpointInfo breakpoint)
                {
                    var goToBreakLine = new MenuItem { Header = new TextBlock { Text = $"Go to Breakpoint ({breakpoint.Location})" } }; // use TextBlock because Location may contain underscores
                    goToBreakLine.Click += (s, _) => NavigationRequested(this, new NagivationEventArgs { GroupIndex = wave.GroupIndex, Breakpoint = breakpoint });
                    menu.Items.Add(goToBreakLine);
                }
                else
                {
                    menu.Items.Add(new MenuItem { Header = "No Breakpoint Hit", IsEnabled = false });
                }
                menu.IsOpen = true;
            }
        }

        private WaveInfo GetWaveAtImagePos(Point p)
        {
            if (_context.Wavemap == null)
                return null;

            var rSize = _context.Options.VisualizerOptions.WavemapElementSize;
            var row = (int)(p.Y / rSize);
            var col = (int)(p.X / rSize) + FirstGroup;
            return _context.Wavemap.GetWaveInfo((uint)col, (uint)row, _context.Options.VisualizerOptions.MaskLanes);
        }

        private void DrawImage()
        {
            var imageContainer = (FrameworkElement)_img.Parent;
            var wavesPerGroup = MathUtils.RoundUpQuotient((int)_context.Options.DebuggerOptions.GroupSize, (int)_context.Options.DebuggerOptions.WaveSize);

            if (_context.Wavemap == null || wavesPerGroup == 0 || imageContainer.ActualHeight == 0)
            {
                _img.Source = null;
                Updated?.Invoke(this, EventArgs.Empty);
                return;
            }

            var rSize = _context.Options.VisualizerOptions.WavemapElementSize;
            GridSizeX = (int)imageContainer.ActualWidth / rSize;
            GridSizeY = wavesPerGroup;

            var pixelCount = GridSizeX * GridSizeY * rSize * rSize;
            var byteCount = pixelCount * 4;
            var imageData = new byte[byteCount + _headerSize];
            _header.CopyTo(imageData, 0);
            var fileSizeBytes = BitConverter.GetBytes(_headerSize + byteCount);
            var widthBytes = BitConverter.GetBytes(GridSizeX * rSize);
            var heightBytes = BitConverter.GetBytes(GridSizeY * rSize);
            var dataSizeBytes = BitConverter.GetBytes(byteCount);

            for (int i = 0; i < 4; i++)
            {
                imageData[2 + i] = fileSizeBytes[i];
                imageData[18 + i] = widthBytes[i];
                imageData[22 + i] = heightBytes[i];
                imageData[34 + i] = dataSizeBytes[i];
            }

            var byteWidth = GridSizeX * rSize * 4;

            for (int i = 0; i < byteCount - 3; i += rSize * 4)
            {
                int row = i / byteWidth;
                int col = i % byteWidth;
                var flatIdx = i + _headerSize;   // header offset

                var waveInfo = GetWaveAtImagePos(new Point(col / 4, GridSizeY * rSize - 1 - row));
                if (waveInfo == null)
                    continue;

                if ((row % rSize) == 0 || (row % rSize) == rSize - 1)
                {
                    for (int rwidth = 0; rwidth < rSize; ++rwidth)
                    {
                        imageData[flatIdx + 3] = 255; // Black
                        flatIdx += 4;
                    }
                }
                else
                {
                    imageData[flatIdx + 3] = 255; // Black
                    flatIdx += 4;

                    for (int rwidth = 1; rwidth < rSize - 1; ++rwidth)
                    {
                        imageData[flatIdx + 0] = waveInfo.BreakColor.B; // B
                        imageData[flatIdx + 1] = waveInfo.BreakColor.G; // G
                        imageData[flatIdx + 2] = waveInfo.BreakColor.R; // R
                        imageData[flatIdx + 3] = waveInfo.BreakColor.A; // Alpha
                        flatIdx += 4;
                    }

                    imageData[flatIdx + 3] = 255; // Black
                }
            }

            _img.Source = LoadImage(imageData);
            Updated?.Invoke(this, EventArgs.Empty);
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
