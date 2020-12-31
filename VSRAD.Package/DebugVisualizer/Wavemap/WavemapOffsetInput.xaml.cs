using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public sealed partial class WavemapOffsetInput : UserControl, INotifyPropertyChanged
    {
        private VisualizerContext _context;
        private WavemapImage _image;
        private int _groupCount;

        public event PropertyChangedEventHandler PropertyChanged;

        private string _rawValue = "No data";
        public string RawValue
        {
            get => _rawValue;
            set => _rawValue = value;
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
            _context.GroupFetched += OnGroupFetched;

            _image = image;
            _image.PropertyChanged += GridSizeChanged;
        }

        private void OnGroupFetched(object sender, GroupFetchedEventArgs e)
        {
            _groupCount = _context.BreakData.GetGroupCount((int)_context.Options.DebuggerOptions.GroupSize, (int)_context.Options.DebuggerOptions.NGroups);
            DecButton.IsEnabled = _image.FirstGroup != 0;
            IncButton.IsEnabled = _image.FirstGroup + _image.GridSizeX < _groupCount;
            _rawValue = $"{_image.FirstGroup} - {_image.FirstGroup + _image.GridSizeX - 1}";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RawValue)));
        }

        private void GridSizeChanged(object sender, PropertyChangedEventArgs e)
        {
            _rawValue = $"{_image.FirstGroup} - {_image.FirstGroup + _image.GridSizeX - 1}";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RawValue)));
            DecButton.IsEnabled = _image.FirstGroup != 0;
            IncButton.IsEnabled = _image.FirstGroup + _image.GridSizeX < _groupCount;
        }
    }
}
