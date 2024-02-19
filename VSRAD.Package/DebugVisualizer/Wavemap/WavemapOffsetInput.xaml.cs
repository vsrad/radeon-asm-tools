using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VSRAD.Package.Server;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public sealed partial class WavemapOffsetInput : UserControl, INotifyPropertyChanged
    {
        private VisualizerContext _context;
        private WavemapImage _image;

        public event PropertyChangedEventHandler PropertyChanged;

        private string _offsetLabel = "No data";
        public string OffsetLabel
        {
            get => _offsetLabel;
            set { _offsetLabel = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OffsetLabel))); }
        }

        private bool _showOffsetSelector = true;
        public bool ShowOffsetSelector
        {
            get => _showOffsetSelector;
            set { _showOffsetSelector = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowOffsetSelector))); }
        }

        public WavemapOffsetInput()
        {
            InitializeComponent();
            // Do not change this.DataContext, it inherits from the control that uses NumberInput
            Root.DataContext = this;
        }

        private void Increment(object sender, RoutedEventArgs e)
        {
            _image.FirstGroup += _image.GridSizeX - _image.FirstGroup % _image.GridSizeX;
        }

        private void Decrement(object sender, RoutedEventArgs e)
        {
            var stepRem = _image.FirstGroup % _image.GridSizeX;
            var dec = stepRem == 0 ? _image.GridSizeX : stepRem;
            _image.FirstGroup = (_image.FirstGroup > dec) ? _image.FirstGroup - dec : 0;
        }

        public void Setup(VisualizerContext context, WavemapImage image)
        {
            _context = context;
            _context.PropertyChanged += UpdateTooltipAndWaveInfo;
            _image = image;
            _image.Updated += UpdateControls;
        }

        private void UpdateTooltipAndWaveInfo(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VisualizerContext.WavemapSelection))
            {
                if (_context.BreakState is BreakState breakState && _context.WavemapSelection is WavemapCell cell)
                {
                    var breakpoint = cell.Wave.BreakpointIndex != null ? breakState.Target.Breakpoints[(int)cell.Wave.BreakpointIndex] : null;
                    {
                        var info = $"G: {cell.GroupIndex}\nW: {cell.WaveIndex}";
                        if (cell.Wave.PartialExec && breakpoint != null)
                            info += " (E)";
                        info += "\n";
                        info += breakpoint != null ? $"L: {breakpoint.Line + 1}" : "No break";
                        WaveInfoTextBlock.Text = info;
                    }
                    {
                        var tooltip = $"Group: {cell.GroupIndex}\nWave: {cell.WaveIndex}";
                        if (cell.Wave.PartialExec && breakpoint != null)
                            tooltip += " (partial EXEC mask)";
                        tooltip += "\n";
                        tooltip += breakpoint != null ? $"Location: {breakpoint.Location}" : "No breakpoint hit";
                        WaveInfoTextBlock.ToolTip = tooltip;
                    }
                }
                else
                {
                    WaveInfoTextBlock.Text = "";
                    WaveInfoTextBlock.ToolTip = "";
                }
            }
        }

        private void UpdateControls(object sender, EventArgs e)
        {
            if (_image.GridSizeX > 0 && _context.BreakState != null && _context.BreakState.NumGroups > 0)
            {
                DecButton.IsEnabled = _image.FirstGroup != 0;
                IncButton.IsEnabled = _image.FirstGroup + _image.GridSizeX < _context.BreakState.NumGroups;

                ShowOffsetSelector = _image.FirstGroup != 0 || IncButton.IsEnabled;

                if (_image.FirstGroup < _context.BreakState.NumGroups)
                {
                    var lastDisplayedGroup = Math.Min(_context.BreakState.NumGroups, _image.FirstGroup + _image.GridSizeX) - 1;
                    OffsetLabel = $"{_image.FirstGroup} - {lastDisplayedGroup}";
                }
                else
                {
                    OffsetLabel = $"{_image.FirstGroup} - {_image.FirstGroup}";
                }
            }
            else
            {
                DecButton.IsEnabled = false;
                IncButton.IsEnabled = false;
                OffsetLabel = "No data";
            }
        }
    }
}
