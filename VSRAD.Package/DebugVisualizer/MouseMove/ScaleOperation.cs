using System;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    public sealed class ScaleOperation : IMouseMoveOperation
    {
        private readonly VisualizerTable _table;

        private int _lastX;
        private int _targetColumnIndex;
        private int _initX;
        private int _initOffset;
        private int _initWidth;
        private int _columnX;
        private int _columnOffset;
        private int _invisibleColumnsBeforeTargetCount;
        private int _invisibleColumnsCount;
        private int _lastVisibleColumn;
        private int _firstVisibleColumn;
        private float _firstColumnVisiblePart;
        private int _firstVisible;
        private int _visibleBeforeFirst;
        private int _currentWidth;

        private const int _maxDistanceFromDivider = 7;

        private bool _operationStarted;

        public ScaleOperation(VisualizerTable table)
        {
            _table = table;
        }

        public bool OperationStarted() => _operationStarted;

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (!_table.DataColumns.Any(x => x.Visible == true)) return false;

            _firstVisibleColumn = _table.DataColumns.First(x => x.Visible == true).Index;

            if (hit.Type != DataGridViewHitTestType.ColumnHeader
                || hit.ColumnIndex < VisualizerTable.DataColumnOffset
                || (((Math.Abs(e.X - hit.ColumnX) > _maxDistanceFromDivider)
                    || hit.ColumnIndex == _firstVisibleColumn) // disable scalling on the left edge of the first element
                    && Math.Abs(e.X - (hit.ColumnX + _table.DataColumns[0].Width)) > _maxDistanceFromDivider))
                return false;

            var invisibleColumns = _table.DataColumns.Where(c => c.Visible == false);

            _lastVisibleColumn = _table.DataColumns.Last(c => c.Visible == true).Index;

            _invisibleColumnsCount = invisibleColumns.Count();
            _invisibleColumnsBeforeTargetCount = invisibleColumns.Count(c => c.Index < hit.ColumnIndex);

            _targetColumnIndex = Math.Max(hit.ColumnIndex - _invisibleColumnsBeforeTargetCount - 1, 0);

            _lastX = _initX = Cursor.Position.X;
            _initWidth = _table.Columns[Math.Max(_targetColumnIndex, VisualizerTable.DataColumnOffset)].Width;
            _initOffset = _table.HorizontalScrollingOffset;
            _columnX = hit.ColumnX - _table.ReservedColumnsOffset;
            if (Math.Abs(e.X - hit.ColumnX) <= _maxDistanceFromDivider)
            {
                _targetColumnIndex -= 1;
                _columnX -= _initWidth;
                _invisibleColumnsBeforeTargetCount = invisibleColumns.Count(c => c.Index < hit.ColumnIndex - 1);
            }
            _columnOffset = _columnX + _initOffset;
            _firstColumnVisiblePart = Math.Abs((((float)_columnX % _initWidth) / _initWidth));
            _firstVisible = _table.FirstDisplayedScrollingColumnIndex;
            _visibleBeforeFirst = _firstVisible - invisibleColumns.Count(c => c.Index <= _firstVisible - 1);
            if (_initOffset == 0)
                _visibleBeforeFirst = 0;
            _operationStarted = false;

            return true;
        }

        public static bool ShouldChangeCursor(DataGridView.HitTestInfo hit, VisualizerTable table, int x) =>
            hit.Type == DataGridViewHitTestType.ColumnHeader &&
            hit.ColumnIndex >= VisualizerTable.DataColumnOffset &&
            ((Math.Abs(x - hit.ColumnX) <= _maxDistanceFromDivider
             && hit.ColumnIndex != table.DataColumns.First(c => c.Visible == true).Index) // disable scalling on the left edge of the first element
             || Math.Abs(x - (hit.ColumnX + table.DataColumns[0].Width)) <= _maxDistanceFromDivider);

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != MouseButtons.Left)
            {
                _table.ColumnResizeController.BeginBulkColumnWidthChange();
                if (!_table.Columns[_firstVisibleColumn].Displayed)
                    _table.Columns[_firstVisibleColumn].Width = _currentWidth;
                if (!_table.Columns[_lastVisibleColumn].Displayed)
                    _table.Columns[_lastVisibleColumn].Width = _currentWidth;
                _table.ColumnResizeController.CommitBulkColumnWidthChange();
                _table.ColumnWidth = _currentWidth;
                return false;
            }
            var x = Cursor.Position.X;
            var diff = x - _lastX;
            ScaleDataColumns(diff, x);
            _lastX = x;
            _operationStarted = true;
            return true;
        }

        private void ScaleDataColumns(int diff, int x)
        {
            if (diff == 0) return;

            _table.ColumnResizeController.BeginBulkColumnWidthChange();

            int scrollingOffset;
            /*
            if (_table.ScalingMode == ScalingMode.ResizeTable)
            {
                var visibleBetweenFirstAndTarget = _targetColumnIndex + 1 - _visibleBeforeFirst + _firstColumnVisiblePart;
                float fullDiff = x - _initX;
                fullDiff /= visibleBetweenFirstAndTarget;
                var width = (int)(_initWidth + fullDiff);
                if (width <= 30 || Math.Abs(fullDiff) < 1.0) return;
                
                scrollingOffset = _initOffset + _visibleBeforeFirst * (int)Math.Floor(fullDiff) - (int)(width * _firstColumnVisiblePart) + (int)(_initWidth * _firstColumnVisiblePart);

                for (int i = VisualizerTable.DataColumnOffset; i < _table.ColumnCount; ++i)
                {
                    if (i == _lastVisibleColumn) continue;
                    _table.Columns[i].Width = width;
                }

                var fullRealWidth = (_table.Columns.Count - _invisibleColumnsCount - 2) * width;
                var lastScreenBegin = Math.Max((_table.ColumnCount - _invisibleColumnsCount - 1) * width - _table.Width - _table.ReservedColumnsOffset, 0);

                if (scrollingOffset > lastScreenBegin)
                {
                    var newWidth = Math.Abs(_table.Width - _columnX - fullRealWidth - _table.ReservedColumnsOffset);
                    _table.Columns[_lastVisibleColumn].Width = newWidth;
                }
                else
                    _table.Columns[_lastVisibleColumn].Width = width;

                if (_table.Columns[_lastVisibleColumn].Width < width)
                    _table.Columns[_lastVisibleColumn].Width = width;
            } 
            else
            {
            */
            var fullDiff = x - _initX;
            var width = _initWidth + fullDiff;
            if (width <= 30) return;

            for (int i = VisualizerTable.DataColumnOffset; i < _table.ColumnCount; ++i)
            {
                if (i == _lastVisibleColumn || i == _firstVisibleColumn) continue;
                _table.Columns[i].Width = width;
            }

            scrollingOffset = _initOffset + _targetColumnIndex * fullDiff;

            // Computing first and last column width
            var realColumnOffset = Math.Abs((_targetColumnIndex - 1) * width);
            var fullRealWidth = (_table.Columns.Count - _targetColumnIndex - _invisibleColumnsCount - 2) * width;
            var lastScreenBegin = Math.Max((_table.ColumnCount - _invisibleColumnsCount - 1) * width - _table.Width - _table.ReservedColumnsOffset, 0);

            if (scrollingOffset <= 0)
                _table.Columns[_firstVisibleColumn].Width = Math.Max(Math.Abs(_columnX - realColumnOffset) - 1, width);
            else
                _table.Columns[_firstVisibleColumn].Width = width;

            if (realColumnOffset != _columnOffset && scrollingOffset > lastScreenBegin)
            {
                var newWidth = Math.Abs(_table.Width - _columnX - fullRealWidth - _table.ReservedColumnsOffset);
                _table.Columns[_lastVisibleColumn].Width = newWidth;
            }
            else
                _table.Columns[_lastVisibleColumn].Width = width;

            if (_table.Columns[_lastVisibleColumn].Width < width)
                _table.Columns[_lastVisibleColumn].Width = width;

            _currentWidth = width;
            // BugFix
            scrollingOffset = Math.Min(scrollingOffset, lastScreenBegin + _table.Width);
            /*
            }
            */
            _table.ColumnResizeController.CommitBulkColumnWidthChange(scrollingOffset);
        }
    }
}
