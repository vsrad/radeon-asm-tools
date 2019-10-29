using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnResizeController
    {
        private delegate void InvalidateCachedColumnsWidths();
        private delegate void PerformLayoutPrivate(bool f1, bool f2, bool f3, bool f4);
        private readonly InvalidateCachedColumnsWidths _invalidateCachedColumnsWidths;
        private readonly PerformLayoutPrivate _performLayoutPrivate;

        private readonly VisualizerTable _table;
        private bool _overrideNativeColumnWidthChangeHandler;

        public ColumnResizeController(VisualizerTable table)
        {
            // Out of the box DataGridView is unable to change the width of even several dozen columns in real time because it recalculates layout after each individual column is resized
            // (https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/DataGridViewMethods.cs,dc107b02a9e367cc)
            // We found that calling just these two methods at the end of scaling is enough to replicate that without performance issues:
            _invalidateCachedColumnsWidths = (InvalidateCachedColumnsWidths)Delegate.CreateDelegate(typeof(InvalidateCachedColumnsWidths), table.Columns,
                typeof(DataGridViewColumnCollection).GetMethod("InvalidateCachedColumnsWidths", BindingFlags.NonPublic | BindingFlags.Instance));
            _performLayoutPrivate = (PerformLayoutPrivate)Delegate.CreateDelegate(typeof(PerformLayoutPrivate), table,
                typeof(DataGridView).GetMethod("PerformLayoutPrivate", BindingFlags.NonPublic | BindingFlags.Instance));

            _table = table;
        }

        public void BeginBulkColumnWidthChange()
        {
            _overrideNativeColumnWidthChangeHandler = true;
        }

        public void CommitBulkColumnWidthChange(int? setScrollingOffsetTo = null)
        {
            _invalidateCachedColumnsWidths();
            _performLayoutPrivate(false, false, false, false);
            _table.Invalidate();
            if (setScrollingOffsetTo is int newScrollingOffset)
                _table.HorizontalScrollingOffset = Math.Max(0, newScrollingOffset);
            _overrideNativeColumnWidthChangeHandler = false;
        }

        public bool HandleColumnWidthChangeEvent() => _overrideNativeColumnWidthChangeHandler;

        public void FitWidth(int scrollingOffsetColumnIndex, int scrollingOffsetColumnRelStart)
        {
            BeginBulkColumnWidthChange();

            var widestColumnWidth = _table.DataColumns.Where(c => c.Visible).Max(c => c.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true));

            foreach (var column in _table.DataColumns)
                column.Width = widestColumnWidth;

            var scrollingOffset = widestColumnWidth * scrollingOffsetColumnIndex - scrollingOffsetColumnRelStart;
            CommitBulkColumnWidthChange(scrollingOffset);
        }
    }
}
