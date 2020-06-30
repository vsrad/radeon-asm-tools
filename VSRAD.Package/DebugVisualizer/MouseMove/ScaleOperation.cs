using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using VSRAD.Package.DebugVisualizer.MouseMove.Scaling;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    public class ScaleState
    {
        public int TableDataAreaWidth;
        public int VisibleColumnsToLeft;
        public int VisibleColumnsToLeftOutOfView;
        public int FirstVisibleIndex;
        public int LastVisibleIndex;
        public int CurrentWidth;
        public float FirstColumnInvisisblePart;
        public float VisibleBetweenFirstAndTarget;
        public DataGridViewColumn TargetColumn;
        public DataGridViewColumn FirstVisibleColumn;

        public bool FirstIsTarget => TargetColumn.Index == FirstVisibleIndex;
    }

    public sealed class ScaleOperation : IMouseMoveOperation
    {
        private readonly DataGridView _table;
        private TableState _tableState;
        private ScaleState _scaleState;

        private ColumnLockScaling _columnLockScaling;
        private ViewLockScaling _viewLockScaling;

        private const int _maxDistanceFromDivider = 7;

        private int _lastX;
        private bool _operationStarted;

        public ScaleOperation(DataGridView table, TableState state)
        {
            _table = table;
            _tableState = state;
            _scaleState = new ScaleState();
            _columnLockScaling = new ColumnLockScaling(_table, _tableState, _scaleState);
            _viewLockScaling = new ViewLockScaling(_table, _tableState, _scaleState);
        }

        public bool OperationStarted() => _operationStarted;

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (!ShouldChangeCursor(hit, _tableState, e.X))
                return false;

            _lastX = Cursor.Position.X;
            _operationStarted = false;

            var index = (Math.Abs(e.X - hit.ColumnX) <= _maxDistanceFromDivider)
                ? hit.ColumnIndex - 1
                : hit.ColumnIndex;
            if (hit.ColumnIndex == _tableState.PhantomColumnIndex)
                index = _scaleState.LastVisibleIndex;
            if (!_table.Columns[index].Visible)
                index = _tableState.DataColumns.Last(c => c.Visible && c.Index < index).Index;

            var firstDisplayedIndex = _tableState.DataColumns.First(c => c.Displayed).Index;

            _scaleState.TableDataAreaWidth = _table.GetRowDisplayRectangle(0, false).Width - _table.RowHeadersWidth;
            _scaleState.VisibleColumnsToLeft = _tableState.DataColumns.Count(c => c.Visible && c.Index < index);
            _scaleState.FirstVisibleIndex = _tableState.GetFirstVisibleDataColumnIndex();
            _scaleState.LastVisibleIndex = _tableState.GetLastVisibleDataColumnIndex();
            _scaleState.VisibleColumnsToLeftOutOfView = _tableState.DataColumns.Count(c => c.Visible && c.Index < _tableState.GetFirstDisplayedColumnIndex());
            _scaleState.TargetColumn = _table.Columns[index];
            _scaleState.FirstVisibleColumn = _table.Columns[_scaleState.FirstVisibleIndex];
            _scaleState.CurrentWidth = _tableState.ColumnWidth;
            _scaleState.FirstColumnInvisisblePart = FirstColumnInvisiblePart();
            _scaleState.VisibleBetweenFirstAndTarget = ((float)(_tableState.DataColumns
                .Where(c => c.Displayed && c.Index < _scaleState.TargetColumn.Index)
                .Sum(c => c.Width)) / _scaleState.CurrentWidth) + (1.0f - _scaleState.FirstColumnInvisisblePart);

#if DEBUG
            _columnLockScaling.SetDebugEdge();
#endif

            return true;
        }

        private float FirstColumnInvisiblePart() =>
            ((float)_table.HorizontalScrollingOffset % _tableState.ColumnWidth) / _tableState.ColumnWidth;

        public static bool ShouldChangeCursor(DataGridView.HitTestInfo hit, TableState state, int x)
        {
            if (hit.Type != DataGridViewHitTestType.ColumnHeader)
                return false;
            if (Math.Abs(x - hit.ColumnX) > _maxDistanceFromDivider && Math.Abs(x - hit.ColumnX - state.ColumnWidth) > _maxDistanceFromDivider)
                return false;

            return true;
        }

        public void NormalizeSpecialColumnsWidth()
        {
            _tableState.ResizeController.BeginBulkColumnWidthChange();
            var offset = _table.HorizontalScrollingOffset;
            // if first column is not displayed - make it as wide as others
            if (!_table.Columns[_scaleState.FirstVisibleIndex].Displayed)
            {
                var firstVisibleWidth = _table.Columns[_scaleState.FirstVisibleIndex].Width;
                _table.Columns[_scaleState.FirstVisibleIndex].Width = _scaleState.CurrentWidth;
                offset -= firstVisibleWidth - _scaleState.CurrentWidth;
            }
            // if first column is dislpayed partly - shrink it
            else if (_table.Columns[_scaleState.FirstVisibleIndex].Width != _scaleState.CurrentWidth
                && offset != 0)
            {
                var initialWidth = _table.Columns[_scaleState.FirstVisibleIndex].Width;
                var fistColumnWidth = Math.Max(_table.Columns[_scaleState.FirstVisibleIndex].Width - offset, _scaleState.CurrentWidth);
                _table.Columns[_scaleState.FirstVisibleIndex].Width = fistColumnWidth;
                offset = _table.Columns[_scaleState.FirstVisibleIndex].Width != _scaleState.CurrentWidth
                    ? 0
                    : _scaleState.CurrentWidth - initialWidth + offset;
            }
            // if phantom column is not displayed - hide it
            if (!_table.Columns[_tableState.PhantomColumnIndex].Displayed)
                _table.Columns[_tableState.PhantomColumnIndex].Width = 2; // minimum width
            // if phantom column is displayed partly - shrink it
            else
            {
                var totalWidth = _tableState.ResizeController.GetTotalWidthInBulkColumnWidthChange();
                var phantomColumnX = totalWidth - _table.Columns[_tableState.PhantomColumnIndex].Width;
                var currentViewEnd = offset + _scaleState.TableDataAreaWidth;
                _table.Columns[_tableState.PhantomColumnIndex].Width = currentViewEnd - phantomColumnX;
            }

            _tableState.ColumnWidth = _scaleState.CurrentWidth;
            _tableState.ResizeController.CommitBulkColumnWidthChange(offset);
        }

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != MouseButtons.Left)
            {
                NormalizeSpecialColumnsWidth();
                return false;
            }
            var x = Cursor.Position.X;
            var diff = x - _lastX;

            switch (_tableState.ScalingMode)
            {
                case ScalingMode.ResizeColumn:
                    _columnLockScaling.ApplyScaling(diff);
                    break;
                default:
                    _viewLockScaling.ApplyScaling(diff);
                    break;
            }

            _lastX = x;
            _operationStarted = true;
            return true;
        }
    }
}
