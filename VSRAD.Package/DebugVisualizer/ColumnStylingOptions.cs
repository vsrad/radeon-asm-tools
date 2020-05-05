using System;
using System.ComponentModel;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnStylingOptions : DefaultNotifyPropertyChanged
    {
        public event Action StylingChanged;

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

        public void ApplyBulkChange(Action change)
        {
            HighlightRegions.RaiseListChangedEvents = false;
            change();
            HighlightRegions.RaiseListChangedEvents = true;
            OnStylingChanged();
        }

        private void OnStylingChanged() => StylingChanged?.Invoke();
    }
}
