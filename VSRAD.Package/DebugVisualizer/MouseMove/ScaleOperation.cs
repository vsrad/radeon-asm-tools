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

            _tableDataAreaWidth = _table.GetRowDisplayRectangle(1, false).Width - _table.RowHeadersWidth;
            _visibleColumnsToLeft = _table.DataColumns.Count(c => c.Visible && c.Index < hit.ColumnIndex);
            _firstVisibleIndex = _table.DataColumns.First(x => x.Visible).Index;
            _lastVisibleIndex = _table.DataColumns.Last(c => c.Visible).Index;
            _targetColumn = _table.Columns[hit.ColumnIndex];

            _debugEdge = DebugEdgePosition();

            return true;
        }

        public static bool ShouldChangeCursor(DataGridView.HitTestInfo hit, VisualizerTable table, int x) =>
            hit.Type == DataGridViewHitTestType.ColumnHeader &&
            Math.Abs(x - hit.ColumnX) <= _maxDistanceFromDivider &&
            hit.ColumnIndex > table.DataColumns.First(c => c.Visible).Index && // can't scale the first visible column
            hit.ColumnIndex < table.DataColumns.Last(c => c.Visible).Index; // can't scale the last visible column

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != MouseButtons.Left)
            {
                //_table.ColumnResizeController.BeginBulkColumnWidthChange();
                //if (!_table.Columns[_firstVisibleColumn].Displayed)
                //    _table.Columns[_firstVisibleColumn].Width = _currentWidth;
                //if (!_table.Columns[_lastVisibleColumn].Displayed)
                //    _table.Columns[_lastVisibleColumn].Width = _currentWidth;
                //_table.ColumnResizeController.CommitBulkColumnWidthChange();
                _table.ColumnWidth = _currentWidth;
                return false;
            }
            var x = Cursor.Position.X;
            var diff = x - _lastX;
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
                if (i == _firstVisibleIndex || i == _lastVisibleIndex) continue;
                _table.Columns[i].Width = width;
            }

            _table.Columns[_firstVisibleIndex].Width += diff;
            if (scrollingOffset < 0)
                _table.Columns[_firstVisibleIndex].Width += Math.Abs(scrollingOffset);

            var maxScrollingOffset = _table.ColumnResizeController.GetTotalWidthInBulkColumnWidthChange() - _tableDataAreaWidth;

            if (scrollingOffset > maxScrollingOffset && _lastVisibleIndex != _targetColumn.Index)
                _table.Columns[_lastVisibleIndex].Width += scrollingOffset - maxScrollingOffset;
            else
                _table.Columns[_lastVisibleIndex].Width += diff;

            _currentWidth = width;

            _table.ColumnResizeController.CommitBulkColumnWidthChange(scrollingOffset);

            var edge = DebugEdgePosition();
            if (_debugEdge != edge)
            {
                System.Diagnostics.Debug.Print($"edge change: {_debugEdge} -> {edge}");
                _debugEdge = edge;
            }
        }

        private int DebugEdgePosition()
        {
            int pos = _table.Columns[_table.FirstDisplayedScrollingColumnIndex].Width - _table.FirstDisplayedScrollingColumnHiddenWidth;
            for (var i = _table.FirstDisplayedScrollingColumnIndex + 1; i < _targetColumn.Index; ++i)
                if (_table.Columns[i].Visible)
                    pos += _table.Columns[i].Width;
            return pos;
        }

        private int _debugEdge;
    }
}
