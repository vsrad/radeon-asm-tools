using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnResizeController
    {
        public bool TableShouldSuppressOnColumnWidthChangedEvent { get; private set; }

        private delegate void InvalidateCachedColumnsWidths();
        private delegate void PerformLayoutPrivate(bool f1, bool f2, bool f3, bool f4);
        private readonly InvalidateCachedColumnsWidths _invalidateCachedColumnsWidths;
        private readonly PerformLayoutPrivate _performLayoutPrivate;

        private readonly TableState _state;

        public ColumnResizeController(TableState state)
        {
            _state = state;
            // Out of the box DataGridView is unable to change the width of even several dozen columns in real time because it recalculates layout after each individual column is resized
            // (https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/DataGridViewMethods.cs,dc107b02a9e367cc)
            // We found that calling just these two methods at the end of scaling is enough to replicate that without performance issues:
            _invalidateCachedColumnsWidths = (InvalidateCachedColumnsWidths)Delegate.CreateDelegate(typeof(InvalidateCachedColumnsWidths), _state.Table.Columns,
                typeof(DataGridViewColumnCollection).GetMethod("InvalidateCachedColumnsWidths", BindingFlags.NonPublic | BindingFlags.Instance));
            _performLayoutPrivate = (PerformLayoutPrivate)Delegate.CreateDelegate(typeof(PerformLayoutPrivate), _state.Table,
                typeof(DataGridView).GetMethod("PerformLayoutPrivate", BindingFlags.NonPublic | BindingFlags.Instance));
        }

        public void BeginBulkColumnWidthChange()
        {
            TableShouldSuppressOnColumnWidthChangedEvent = true;
        }

        public int GetTotalWidthInBulkColumnWidthChange()
        {
            _invalidateCachedColumnsWidths();
            return _state.Table.Columns.GetColumnsWidth(DataGridViewElementStates.Visible);
        }

        public void CommitBulkColumnWidthChange(int? setScrollingOffsetTo = null)
        {
            _invalidateCachedColumnsWidths();
            _performLayoutPrivate(false, false, false, false);
            _state.Table.Invalidate();
            if (setScrollingOffsetTo is int newScrollingOffset)
                _state.Table.HorizontalScrollingOffset = Math.Max(0, newScrollingOffset);
            TableShouldSuppressOnColumnWidthChangedEvent = false;
        }

        public void FitWidth(int scrollingOffsetColumnIndex, int scrollingOffsetColumnRelStart)
        {
            BeginBulkColumnWidthChange();

            var widestColumnWidth = _state.DataColumns.Where(c => c.Visible)
                .Max(c => c.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true));

            foreach (var column in _state.DataColumns)
                column.Width = widestColumnWidth;

            var scrollingOffset = widestColumnWidth * scrollingOffsetColumnIndex - scrollingOffsetColumnRelStart;
            CommitBulkColumnWidthChange(scrollingOffset);
        }
    }
}
