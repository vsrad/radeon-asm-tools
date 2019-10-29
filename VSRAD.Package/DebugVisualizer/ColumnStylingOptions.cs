using Newtonsoft.Json;
using System;
using System.ComponentModel;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnStylingOptions : DefaultNotifyPropertyChanged
    {
        public event Action StylingChanged;

        private ColumnStyling _computed;
        [JsonIgnore]
        public ColumnStyling Computed
        {
            get
            {
                if (_computed == null)
                {
                    _computed = new ColumnStyling(VisibleColumns, HighlightRegions);
                }
                return _computed;
            }
        }

        private string _visibleColumns = "0:1-511";
        public string VisibleColumns
        {
            get => _visibleColumns;
            set
            {
                SetField(ref _visibleColumns, value);
                OnStylingChanged();
            }
        }

        public BindingList<ColumnHighlightRegion> HighlightRegions { get; } =
            new BindingList<ColumnHighlightRegion>();

        public ColumnStylingOptions()
        {
            HighlightRegions.ListChanged += (sender, args) => OnStylingChanged();
        }

        private void OnStylingChanged()
        {
            _computed = null;
            StylingChanged?.Invoke();
        }
    }
}
