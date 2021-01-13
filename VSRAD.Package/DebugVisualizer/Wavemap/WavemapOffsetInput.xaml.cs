using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public sealed partial class WavemapOffsetInput : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty WaveInfoProperty =
            DependencyProperty.Register(nameof(WaveInfo), typeof(string), typeof(WavemapOffsetInput),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string WaveInfo
        {
            get => (string)GetValue(WaveInfoProperty);
            set => SetValue(WaveInfoProperty, value);
        }

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
            _image = image;
            _image.Updated += UpdateControls;
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
