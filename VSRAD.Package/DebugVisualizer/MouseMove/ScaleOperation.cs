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
        public int FirstVisibleIndex;
        public int LastVisibleIndex;
        public int CurrentWidth;
        public DataGridViewColumn TargetColumn;

        public bool FirstIsTarget => TargetColumn.Index == FirstVisibleIndex;
    }

    public sealed class ScaleOperation : IMouseMoveOperation
    {
        private readonly DataGridView _table;
        private TableState _tableState;
        private ScaleState _scaleState;

        private ColumnLockScaling _columnLockScaling;

        private const int _maxDistanceFromDivider = 7;

        private int _lastX;
        private bool _operationStarted;

        public ScaleOperation(DataGridView table, TableState state)
        {
            _table = table;
            _tableState = state;
            _scaleState = new ScaleState();
            _columnLockScaling = new ColumnLockScaling(_table, _tableState, _scaleState);
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

            _scaleState.TableDataAreaWidth = _table.GetRowDisplayRectangle(0, false).Width - _table.RowHeadersWidth;
            _scaleState.VisibleColumnsToLeft = _tableState.DataColumns.Count(c => c.Visible && c.Index < index);
            _scaleState.FirstVisibleIndex = _tableState.GetFirstVisibleDataColumnIndex();
            _scaleState.LastVisibleIndex = _tableState.GetLastVisibleDataColumnIndex();
            _scaleState.TargetColumn = _table.Columns[index];
            _scaleState.CurrentWidth = _tableState.ColumnWidth;

#if DEBUG
            _columnLockScaling.SetDebugEdge();
#endif

            return true;
        }

        public static bool ShouldChangeCursor(DataGridView.HitTestInfo hit, TableState state, int x)
        {
            if (hit.Type != DataGridViewHitTestType.ColumnHeader)
                return false;
            if (Math.Abs(x - hit.ColumnX) > _maxDistanceFromDivider && Math.Abs(x - hit.ColumnX - state.ColumnWidth) > _maxDistanceFromDivider)
                return false;

            // can't scale the first visible column
            var firstVisibleIndex = state.GetFirstVisibleDataColumnIndex();
            return firstVisibleIndex != -1 && hit.ColumnIndex > firstVisibleIndex;
        }

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != MouseButtons.Left)
            {
                // Check scaling mode
                _columnLockScaling.NormalizeSpecialColumnsWidth();
                return false;
            }
            var x = Cursor.Position.X;
            var diff = x - _lastX;

            // Check scaling mode
            _columnLockScaling.ApplyScaling(diff);

            _lastX = x;
            _operationStarted = true;
            return true;
        }
    }
}
