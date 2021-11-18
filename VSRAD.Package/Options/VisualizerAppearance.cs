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

        private ContentAlignment _headersAlignment = ContentAlignment.Right;
        public ContentAlignment HeadersAlignment
        {
            get => _headersAlignment;
            set => SetField(ref _headersAlignment, value);
        }

        private ContentAlignment _dataColumnAlignment = ContentAlignment.Right;
        public ContentAlignment DataColumnAlignment
        {
            get => _dataColumnAlignment;
            set => SetField(ref _dataColumnAlignment, value);
        }
        #endregion
        #region diviers
        private uint _laneGrouping;
        public uint LaneGrouping { get => _laneGrouping; set => SetField(ref _laneGrouping, value); }

        private bool _verticalSplit = true;
        public bool VerticalSplit { get => _verticalSplit; set => SetField(ref _verticalSplit, value); }

        private int _laneSeparatorWidth = 3;
        public int LaneSeparatorWidth
        {
            get => _laneSeparatorWidth;
            set => SetField(ref _laneSeparatorWidth, value);
        }

        private int _hiddenColumnSeparatorWidth = 8;
        public int HiddenColumnSeparatorWidth
        {
            get => _hiddenColumnSeparatorWidth;
            set => SetField(ref _hiddenColumnSeparatorWidth, value);
        }
        #endregion
        #region number separators
        private uint _binHexSeparator;
        public uint BinHexSeparator { get => _binHexSeparator; set => SetField(ref _binHexSeparator, value); }

        private uint _intUintSeparator;
        public uint IntUintSeparator { get => _intUintSeparator; set => SetField(ref _intUintSeparator, value); }

        private bool _binHexLeadingZeroes;
        public bool BinHexLeadingZeroes { get => _binHexLeadingZeroes; set => SetField(ref _binHexLeadingZeroes, value); }
        #endregion
        private ScalingMode _scalingMode = ScalingMode.ResizeColumn;

        public ScalingMode ScalingMode
        {
            get => _scalingMode;
            set => SetField(ref _scalingMode, value);
        }

        private bool _scaleNameColumn = true;

        public bool ScaleNameColumn { get => _scaleNameColumn; set => SetField(ref _scaleNameColumn, value); }

        private int _darkenAlternatingRowsBy = 0;
        public int DarkenAlternatingRowsBy { get => _darkenAlternatingRowsBy; set => SetField(ref _darkenAlternatingRowsBy, value); }
    }
}
