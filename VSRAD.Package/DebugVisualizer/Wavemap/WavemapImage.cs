using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public sealed class WavemapImage
    {
        public static readonly System.Drawing.Color Blue = System.Drawing.Color.FromArgb(69, 115, 167);
        public static readonly System.Drawing.Color Red = System.Drawing.Color.FromArgb(172, 69, 70);
        public static readonly System.Drawing.Color Green = System.Drawing.Color.FromArgb(137, 166, 76);
        public static readonly System.Drawing.Color Violet = System.Drawing.Color.FromArgb(112, 89, 145);
        public static readonly System.Drawing.Color Pink = System.Drawing.Color.FromArgb(208, 147, 146);
        public static readonly System.Drawing.Color[] BreakpointColors = new[] { Blue, Red, Green, Violet, Pink };
        public static readonly System.Drawing.Color NoBreakpointColor = System.Drawing.Color.Gray;

        public event EventHandler<VisualizerNavigationEventArgs> NavigationRequested;

        public event EventHandler Updated;

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

        private readonly Image _imageControl;
        private readonly VisualizerContext _context;

        public WavemapImage(Image image, VisualizerContext context)
        {
            _imageControl = image;
            _imageControl.MouseMove += ShowWaveInfo;
            _imageControl.MouseLeftButtonUp += NavigateToWave;
            _imageControl.MouseRightButtonUp += ShowWaveMenu;
            _imageControl.MouseLeave += HideWaveInfo;

            _context = context;
            _context.PropertyChanged += VisualizerStateChanged;
            _context.Options.VisualizerOptions.PropertyChanged += VisualizerStateChanged;

            ((FrameworkElement)_imageControl.Parent).SizeChanged += RecomputeGridSize;
        }

        private void VisualizerStateChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(VisualizerContext.BreakState):
                case nameof(VisualizerContext.WavemapSelection):
                case nameof(Options.VisualizerOptions.MaskLanes):
                case nameof(Options.VisualizerOptions.WavemapElementSize):
                    DrawImage();
                    break;
            }
        }

        private void RecomputeGridSize(object sender, SizeChangedEventArgs e)
        {
            var rSize = _context.Options.VisualizerOptions.WavemapElementSize;
            var newGridSizeX = (int)((FrameworkElement)_imageControl.Parent).ActualWidth / rSize;
            if (newGridSizeX != GridSizeX)
                DrawImage();
        }

        private void ShowWaveInfo(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _context.WavemapSelection = GetCellAtImagePos(e.GetPosition(_imageControl));
        }

        private void NavigateToWave(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GetCellAtImagePos(e.GetPosition(_imageControl)) is WavemapCell cell)
            {
                var breakpoint = cell.Wave.BreakpointIndex != null ? _context.BreakState.Target.Breakpoints[(int)cell.Wave.BreakpointIndex] : null;
                NavigationRequested(this, new VisualizerNavigationEventArgs { GroupIndex = cell.GroupIndex, WaveIndex = cell.WaveIndex, Breakpoint = breakpoint });
            }
        }

        private void HideWaveInfo(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _context.WavemapSelection = null;
        }

        private void ShowWaveMenu(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GetCellAtImagePos(e.GetPosition(_imageControl)) is WavemapCell cell)
            {
                var menu = new ContextMenu { PlacementTarget = _imageControl };
                var goToGroup = new MenuItem { Header = $"Go to Group #{cell.GroupIndex}" };
                goToGroup.Click += (s, _) => NavigationRequested(this, new VisualizerNavigationEventArgs { GroupIndex = cell.GroupIndex });
                menu.Items.Add(goToGroup);
                var goToWave = new MenuItem { Header = $"Go to Wave #{cell.WaveIndex}" };
                goToWave.Click += (s, _) => NavigationRequested(this, new VisualizerNavigationEventArgs { WaveIndex = cell.WaveIndex });
                menu.Items.Add(goToWave);
                if (cell.Wave.BreakpointIndex != null)
                {
                    var breakpoint = _context.BreakState.Target.Breakpoints[(int)cell.Wave.BreakpointIndex];
                    var goToBreakLine = new MenuItem { Header = new TextBlock { Text = $"Go to Breakpoint ({breakpoint.Location})" } }; // use TextBlock because Location may contain underscores
                    goToBreakLine.Click += (s, _) => NavigationRequested(this, new VisualizerNavigationEventArgs { Breakpoint = breakpoint });
                    menu.Items.Add(goToBreakLine);
                }
                else
                {
                    menu.Items.Add(new MenuItem { Header = "No Breakpoint Hit", IsEnabled = false });
                }
                menu.IsOpen = true;
            }
        }

        private WavemapCell? GetCellAtImagePos(Point p)
        {
            var cellSize = _context.Options.VisualizerOptions.WavemapElementSize;
            uint waveIndex = (uint)(p.Y / cellSize);
            uint groupIndex = (uint)(p.X / cellSize) + FirstGroup;
            if (_context.BreakState is BreakState breakState && waveIndex < breakState.WavesPerGroup && groupIndex < breakState.NumGroups)
                return new WavemapCell(waveIndex: waveIndex, groupIndex: groupIndex, breakState.WaveStatus[(int)(breakState.WavesPerGroup * groupIndex + waveIndex)]);
            else
                return null;
        }

        private void DrawImage()
        {
            var imageContainer = (FrameworkElement)_imageControl.Parent;
            if (_context.BreakState == null || imageContainer.ActualHeight == 0)
            {
                _imageControl.Source = null;
                Updated?.Invoke(this, EventArgs.Empty);
                return;
            }

            var cellSize = _context.Options.VisualizerOptions.WavemapElementSize;
            GridSizeX = (uint)(imageContainer.ActualWidth / cellSize);
            GridSizeY = _context.BreakState.WavesPerGroup;

            var (pixelWidth, pixelHeight) = (GridSizeX * cellSize, GridSizeY * cellSize);
            var pixelCount = pixelWidth * pixelHeight;
            var pixelData = new byte[pixelCount * 4]; // BGRA

            var breakpointColorMapping = new Dictionary<uint, System.Drawing.Color>();
            var currentColorIndex = 0;

            for (uint p = 0; p < pixelCount; p += cellSize)
            {
                uint row = p / pixelWidth;
                uint col = p % pixelWidth;
                var byteIdx = p * 4;

                if (GetCellAtImagePos(new Point(col, row)) is WavemapCell cell)
                {
                    var cellColor = NoBreakpointColor;
                    if (cell.Wave.BreakpointIndex is uint breakpointIndex)
                    {
                        if (!breakpointColorMapping.TryGetValue(breakpointIndex, out cellColor))
                        {
                            cellColor = BreakpointColors[currentColorIndex];
                            currentColorIndex = (currentColorIndex + 1) % BreakpointColors.Length;
                            breakpointColorMapping.Add(breakpointIndex, cellColor);
                        }
                        if (_context.Options.VisualizerOptions.MaskLanes && cell.Wave.PartialExec)
                            cellColor = cellColor.ScaleLightness(0.75f);
                    }
                    if (cell == _context.WavemapSelection)
                        cellColor = cellColor.ScaleLightness(1.375f);

                    var cellRow = row % cellSize;
                    if (cellRow == 0 || cellRow == cellSize - 1)
                    {
                        for (var r = 0; r < cellSize; ++r)
                        {
                            pixelData[byteIdx + 3] = 255; // Black
                            byteIdx += 4;
                        }
                    }
                    else
                    {
                        pixelData[byteIdx + 3] = 255; // Black
                        byteIdx += 4;

                        for (var r = 1; r < cellSize - 1; ++r)
                        {
                            pixelData[byteIdx + 0] = cellColor.B;
                            pixelData[byteIdx + 1] = cellColor.G;
                            pixelData[byteIdx + 2] = cellColor.R;
                            pixelData[byteIdx + 3] = cellColor.A;
                            byteIdx += 4;
                        }

                        pixelData[byteIdx + 3] = 255; // Black
                    }
                }
            }

            if (_imageControl.Source is WriteableBitmap b && b.PixelWidth == pixelWidth && b.PixelHeight == pixelHeight)
            {
                b.WritePixels(new Int32Rect(0, 0, b.PixelWidth, b.PixelHeight), pixelData, b.PixelWidth * 4, 0);
            }
            else
            {
                b = new WriteableBitmap((int)pixelWidth, (int)pixelHeight, 96, 96, PixelFormats.Bgra32, null);
                b.WritePixels(new Int32Rect(0, 0, b.PixelWidth, b.PixelHeight), pixelData, b.PixelWidth * 4, 0);
                _imageControl.Source = b;
            }
            Updated?.Invoke(this, EventArgs.Empty);
        }
    }
}
