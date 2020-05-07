using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public sealed class VisualizerAppearance : DefaultNotifyPropertyChanged
    {
        #region alignment
        private ContentAlignment _nameColumnAlignment = ContentAlignment.Left;
        public ContentAlignment NameColumnAlignment
        {
            get => _nameColumnAlignment;
            set => SetField(ref _nameColumnAlignment, value);
        }

        private ContentAlignment _headersAlignment = ContentAlignment.Left;
        public ContentAlignment HeadersAlignment
        {
            get => _headersAlignment;
            set => SetField(ref _headersAlignment, value);
        }

        private ContentAlignment _dataColumnAlignment = ContentAlignment.Left;
        public ContentAlignment DataColumnAlignment
        {
            get => _dataColumnAlignment;
            set => SetField(ref _dataColumnAlignment, value);
        }
        #endregion
        #region diviers
        private int _laneDividerWidth = 3;
        public int LaneDivierWidth
        {
            get => _laneDividerWidth;
            set => SetField(ref _laneDividerWidth, value);
        }

        private int _hiddenColumnSeparatorWidth = 8;
        public int HiddenColumnSeparatorWidth
        {
            get => _hiddenColumnSeparatorWidth;
            set => SetField(ref _hiddenColumnSeparatorWidth, value);
        }
        #endregion
        private ScalingMode _scalingMode = ScalingMode.ResizeColumn;

        public ScalingMode ScalingMode
        {
            get => _scalingMode;
            set => SetField(ref _scalingMode, value);
        }
    }
}
