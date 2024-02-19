using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;

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
        public static readonly System.Drawing.Color Blue = System.Drawing.Color.FromArgb(69, 115, 167);
        public static readonly System.Drawing.Color Red = System.Drawing.Color.FromArgb(172, 69, 70);
        public static readonly System.Drawing.Color Green = System.Drawing.Color.FromArgb(137, 166, 76);
        public static readonly System.Drawing.Color Violet = System.Drawing.Color.FromArgb(112, 89, 145);
        public static readonly System.Drawing.Color Pink = System.Drawing.Color.FromArgb(208, 147, 146);
        public static readonly System.Drawing.Color[] BreakpointColors = new[] { Blue, Red, Green, Violet, Pink };

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

        private uint _gridSizeX;
        public uint GridSizeX
        {
            get => _gridSizeX;
            private set { if (value >= 8) _gridSizeX = value; }
        }

        public uint GridSizeY { get; private set; }

        private uint _firstGroup;
        public uint FirstGroup
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
                case nameof(VisualizerContext.BreakState):
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
            _context.WavemapSelection = GetCellAtImagePos(e.GetPosition(_img));
        }

        private void ShowWaveMenu(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var cell = GetCellAtImagePos(e.GetPosition(_img));
            if (cell != null)
            {
                var menu = new ContextMenu { PlacementTarget = _img };
                var goToGroup = new MenuItem { Header = $"Go to Group #{cell.GroupIndex}" };
                goToGroup.Click += (s, _) => NavigationRequested(this, new NagivationEventArgs { GroupIndex = cell.GroupIndex });
                menu.Items.Add(goToGroup);
                var goToWave = new MenuItem { Header = $"Go to Wave #{cell.WaveIndex} of Group #{cell.GroupIndex}" };
                goToWave.Click += (s, _) => NavigationRequested(this, new NagivationEventArgs { GroupIndex = cell.GroupIndex, WaveIndex = cell.WaveIndex });
                menu.Items.Add(goToWave);
                if (cell.Wave.BreakpointIndex != null)
                {
                    var breakpoint = _context.BreakState.Target.Breakpoints[(int)cell.Wave.BreakpointIndex];
                    var goToBreakLine = new MenuItem { Header = new TextBlock { Text = $"Go to Breakpoint ({breakpoint.Location})" } }; // use TextBlock because Location may contain underscores
                    goToBreakLine.Click += (s, _) => NavigationRequested(this, new NagivationEventArgs { GroupIndex = cell.GroupIndex, Breakpoint = breakpoint });
                    menu.Items.Add(goToBreakLine);
                }
                else
                {
                    menu.Items.Add(new MenuItem { Header = "No Breakpoint Hit", IsEnabled = false });
                }
                menu.IsOpen = true;
            }
        }

        private WavemapCell GetCellAtImagePos(Point p)
        {
            var rSize = _context.Options.VisualizerOptions.WavemapElementSize;
            uint waveIndex = (uint)(p.Y / rSize);
            uint groupIndex = (uint)(p.X / rSize) + FirstGroup;
            if (_context.BreakState is BreakState breakState && waveIndex < breakState.WavesPerGroup && groupIndex < breakState.NumGroups)
                return new WavemapCell(waveIndex: waveIndex, groupIndex: groupIndex, breakState.WaveStatus[(int)(breakState.WavesPerGroup * groupIndex + waveIndex)]);
            else
                return null;
        }

        private void DrawImage()
        {
            var imageContainer = (FrameworkElement)_img.Parent;

            if (_context.BreakState == null || imageContainer.ActualHeight == 0)
            {
                _img.Source = null;
                Updated?.Invoke(this, EventArgs.Empty);
                return;
            }

            var breakpointColorMapping = new Dictionary<uint, System.Drawing.Color>();
            var currentColorIndex = 0;

            var rSize = _context.Options.VisualizerOptions.WavemapElementSize;
            GridSizeX = (uint)(imageContainer.ActualWidth / rSize);
            GridSizeY = _context.BreakState.WavesPerGroup;

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

            for (uint i = 0; i < byteCount - 3; i += rSize * 4)
            {
                uint row = i / byteWidth;
                uint col = i % byteWidth;
                var flatIdx = i + _headerSize;   // header offset

                var cell = GetCellAtImagePos(new Point(col / 4, GridSizeY * rSize - 1 - row));
                if (cell == null)
                    continue;

                var breakColor = System.Drawing.Color.Gray;
                if (cell.Wave.BreakpointIndex is uint breakpointIndex)
                {
                    if (!breakpointColorMapping.TryGetValue(breakpointIndex, out breakColor))
                    {
                        breakColor = BreakpointColors[currentColorIndex];
                        currentColorIndex = (currentColorIndex + 1) % BreakpointColors.Length;
                        breakpointColorMapping.Add(breakpointIndex, breakColor);
                    }
                    if (_context.Options.VisualizerOptions.MaskLanes && cell.Wave.PartialExec)
                        breakColor = System.Drawing.Color.FromArgb(breakColor.R / 2, breakColor.G / 2, breakColor.B / 2);
                }

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
                        imageData[flatIdx + 0] = breakColor.B;
                        imageData[flatIdx + 1] = breakColor.G;
                        imageData[flatIdx + 2] = breakColor.R;
                        imageData[flatIdx + 3] = breakColor.A;
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
