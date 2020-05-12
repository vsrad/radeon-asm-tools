using System;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    public sealed class ScaleOperation : IMouseMoveOperation
    {
        private readonly VisualizerTable _table;

        private const int _maxDistanceFromDivider = 7;

        private int _lastX;
        private bool _operationStarted;

        private int _tableDataAreaWidth;
        private int _visibleColumnsToLeft;
        private int _firstVisibleIndex;
        private int _lastVisibleIndex;
        private int _currentWidth;

        private DataGridViewColumn _targetColumn;

        public ScaleOperation(VisualizerTable table)
        {
            _table = table;
        }

        public bool OperationStarted() => _operationStarted;

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (!ShouldChangeCursor(hit, _table, e.X))
                return false;

            _lastX = Cursor.Position.X;
            _operationStarted = false;

            var index = (Math.Abs(e.X - hit.ColumnX) <= _maxDistanceFromDivider)
                ? hit.ColumnIndex - 1
                : hit.ColumnIndex;
            if (hit.ColumnIndex == VisualizerTable.PhantomColumnIndex)
                index = _lastVisibleIndex;
            if (!_table.Columns[index].Visible)
                index = _table.DataColumns.Last(c => c.Visible && c.Index < index).Index;

            _tableDataAreaWidth = _table.GetRowDisplayRectangle(1, false).Width - _table.RowHeadersWidth;
            _visibleColumnsToLeft = _table.DataColumns.Count(c => c.Visible && c.Index < index);
            _firstVisibleIndex = _table.DataColumns.First(x => x.Visible).Index;
            _lastVisibleIndex = _table.DataColumns.Last(c => c.Visible).Index;
            _targetColumn = _table.Columns[index];
            _currentWidth = _table.ColumnWidth;

#if DEBUG
            _debugEdge = DebugEdgePosition();
#endif

            return true;
        }

        public static bool ShouldChangeCursor(DataGridView.HitTestInfo hit, VisualizerTable table, int x) =>
            hit.Type == DataGridViewHitTestType.ColumnHeader &&
            (Math.Abs(x - hit.ColumnX) <= _maxDistanceFromDivider ||
            Math.Abs(x - hit.ColumnX - table.ColumnWidth) <= _maxDistanceFromDivider) &&
            hit.ColumnIndex > table.DataColumns.FirstOrDefault(c => c.Visible)?.Index; // can't scale the first visible column

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != MouseButtons.Left)
            {
                NormalizeSpecialColumnsWidth();
                return false;
            }
            var x = Cursor.Position.X;
            var diff = x - _lastX;
            if (_table.DataColumns.Count(c => c.Visible) == 1)
                ScaleOneDataColumn(diff);
            else if (_targetColumn.Index == _firstVisibleIndex)
                ScaleDataColumnsWithFirstVisibleAsTarget(diff);
            else
                ScaleDataColumns(diff);
            _lastX = x;
            _operationStarted = true;
            return true;
        }

        private void ScaleDataColumns(int diff)
        {
            var width = _targetColumn.Width + diff;
            if (diff == 0 || width < 30)
                return;

            _table.ColumnResizeController.BeginBulkColumnWidthChange();

            var scrollingOffset = _table.HorizontalScrollingOffset + diff * _visibleColumnsToLeft;

            for (int i = VisualizerTable.DataColumnOffset; i < _table.ColumnCount; ++i)
            {
                if (i == _firstVisibleIndex || i == VisualizerTable.PhantomColumnIndex) continue;
                _table.Columns[i].Width = width;
            }

            _table.Columns[_firstVisibleIndex].Width += diff;
            if (scrollingOffset < 0)
                _table.Columns[_firstVisibleIndex].Width += Math.Abs(scrollingOffset);

            var maxScrollingOffset = _table.ColumnResizeController.GetTotalWidthInBulkColumnWidthChange() - _tableDataAreaWidth;

            if (scrollingOffset > maxScrollingOffset)
                _table.Columns[VisualizerTable.PhantomColumnIndex].Width += scrollingOffset - maxScrollingOffset;

            _currentWidth = width;

            _table.ColumnResizeController.CommitBulkColumnWidthChange(scrollingOffset);

#if DEBUG
            var edge = DebugEdgePosition();
            if (_debugEdge != edge)
            {
                System.Diagnostics.Debug.Print($"edge change: {_debugEdge} -> {edge}");
                _debugEdge = edge;
            }
#endif
        }

        private void ScaleOneDataColumn(int diff)
        {
            if (diff == 0 || (_table.Columns[_firstVisibleIndex].Width < 30) && diff < 0)
                return;
            _table.Columns[_firstVisibleIndex].Width += diff;
            var totalWidth = _table.ColumnResizeController.GetTotalWidthInBulkColumnWidthChange();
            _table.Columns[VisualizerTable.PhantomColumnIndex].Width += _tableDataAreaWidth - totalWidth;
            _table.ColumnResizeController.BeginBulkColumnWidthChange();
            for (int i = VisualizerTable.DataColumnOffset; i < _table.ColumnCount; ++i)
            {
                if (i == _firstVisibleIndex || i == VisualizerTable.PhantomColumnIndex) continue;
                _table.Columns[i].Width = _table.Columns[_firstVisibleIndex].Width;
            }
            _currentWidth = _table.Columns[_firstVisibleIndex].Width;
            _table.ColumnResizeController.CommitBulkColumnWidthChange();
        }

        private void ScaleDataColumnsWithFirstVisibleAsTarget(int diff)
        {
            var width = _currentWidth + diff;
            if (diff == 0 || width < 30)
                return;
            if (_table.Columns[_firstVisibleIndex].Width <= _currentWidth)
            {
                ScaleDataColumns(diff);
                return;
            }
            else
            {
                _table.Columns[_firstVisibleIndex].Width += diff;
                if (diff > 0)
                {
                    for (int i = VisualizerTable.DataColumnOffset; i < _table.ColumnCount; ++i)
                    {
                        if (i == _firstVisibleIndex) continue;
                        _table.Columns[i].Width = width;
                    }
                    _currentWidth = width;
                }
            }
        }

        private void NormalizeSpecialColumnsWidth()
        {
            _table.ColumnResizeController.BeginBulkColumnWidthChange();
            var offset = _table.HorizontalScrollingOffset;
            // if first column is not displayed - make it as wide as others
            if (!_table.Columns[_firstVisibleIndex].Displayed)
            {
                var firstVisibleWidth = _table.Columns[_firstVisibleIndex].Width;
                _table.Columns[_firstVisibleIndex].Width = _currentWidth;
                offset -= firstVisibleWidth - _currentWidth;
            }
            // if first column is dislpayed partly - shrink it
            else if (_table.Columns[_firstVisibleIndex].Width != _currentWidth
                && offset != 0)
            {
                var initialWidth = _table.Columns[_firstVisibleIndex].Width;
                var fistColumnWidth = Math.Max(_table.Columns[_firstVisibleIndex].Width - offset, _currentWidth);
                _table.Columns[_firstVisibleIndex].Width = fistColumnWidth;
                offset = _table.Columns[_firstVisibleIndex].Width != _currentWidth
                    ? 0
                    : _currentWidth - initialWidth + offset;
            }
            // if phantom column is not displayed - hide it
            if (!_table.Columns[VisualizerTable.PhantomColumnIndex].Displayed)
                _table.Columns[VisualizerTable.PhantomColumnIndex].Width = 2; // minimum width
            // if phantom column is displayed partly - shrink it
            else
            {
                var totalWidth = _table.ColumnResizeController.GetTotalWidthInBulkColumnWidthChange();
                var phantomColumnX = totalWidth - _table.Columns[VisualizerTable.PhantomColumnIndex].Width;
                var currentViewEnd = offset + _tableDataAreaWidth;
                _table.Columns[VisualizerTable.PhantomColumnIndex].Width = currentViewEnd - phantomColumnX;
            }

            _table.ColumnWidth = _currentWidth;
            _table.ColumnResizeController.CommitBulkColumnWidthChange(offset);
        }

#if DEBUG
        private int DebugEdgePosition()
        {
            int pos = _table.Columns[_table.FirstDisplayedScrollingColumnIndex].Width - _table.FirstDisplayedScrollingColumnHiddenWidth;
            for (var i = _table.FirstDisplayedScrollingColumnIndex + 1; i < _targetColumn.Index; ++i)
                if (_table.Columns[i].Visible)
                    pos += _table.Columns[i].Width;
            return pos;
        }

        private int _debugEdge;
#endif
    }
}
