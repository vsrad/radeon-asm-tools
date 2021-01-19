using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

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
            if (e.PropertyName == nameof(VisualizerContext.CurrentWaveBreakLine) ||
                e.PropertyName == nameof(VisualizerContext.CurrentWaveGroupIndex) ||
                e.PropertyName == nameof(VisualizerContext.CurrentWaveIndex) ||
                e.PropertyName == nameof(VisualizerContext.CurrentWavePartialMask) ||
                e.PropertyName == nameof(VisualizerContext.CurrentWaveBreakNotRiched))
            {
                var waveInfo = $"G: {_context.CurrentWaveGroupIndex}\nW: {_context.CurrentWaveIndex}";
                if (_context.CurrentWavePartialMask && !_context.CurrentWaveBreakNotRiched) waveInfo += " (E)";
                waveInfo += "\n";
                waveInfo += _context.CurrentWaveBreakNotRiched
                    ? "no brk"
                    : $"L: {_context.CurrentWaveBreakLine}";

                var tooltip = $"Group: {_context.CurrentWaveGroupIndex}\nWave: {_context.CurrentWaveIndex}";
                if (_context.CurrentWavePartialMask && !_context.CurrentWaveBreakNotRiched) tooltip += " (partial mask)";
                tooltip += "\n";
                tooltip += _context.CurrentWaveBreakNotRiched
                    ? "Brk point not reached"
                    : $"Line: {_context.CurrentWaveBreakLine}";

                WaveInfoTextBlock.Text = waveInfo;
                WaveInfoTextBlock.ToolTip = tooltip;
            }
        }

        private void UpdateControls(object sender, EventArgs e)
        {
            var groupCount = _context.BreakData?.GetGroupCount((int)_context.Options.DebuggerOptions.GroupSize, (int)_context.Options.DebuggerOptions.NGroups) ?? 0;
            if (groupCount > 0 && _image.GridSizeX > 0)
            {
                DecButton.IsEnabled = _image.FirstGroup != 0;
                IncButton.IsEnabled = _image.FirstGroup + _image.GridSizeX < groupCount;
                OffsetLabel = $"{_image.FirstGroup} - {_image.FirstGroup + _image.GridSizeX - 1}";
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
