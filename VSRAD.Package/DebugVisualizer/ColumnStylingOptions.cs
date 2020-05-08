using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnStylingOptions : DefaultNotifyPropertyChanged
    {
        private string _visibleColumns = "0:1-511";
        public string VisibleColumns { get => _visibleColumns; set => SetField(ref _visibleColumns, value); }

        private string _backgroundColors;
        public string BackgroundColors { get => _backgroundColors; set => SetField(ref _backgroundColors, value); }

        private string _foregroundColors;
        public string ForegroundColors { get => _foregroundColors; set => SetField(ref _foregroundColors, value); }
    }
}
