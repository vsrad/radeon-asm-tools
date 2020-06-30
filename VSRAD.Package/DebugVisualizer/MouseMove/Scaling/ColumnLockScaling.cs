using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove.Scaling
{
    class ColumnLockScaling
    {
        private readonly DataGridView _table;
        private TableState _tableState;
        private ScaleState _scaleState;

        public ColumnLockScaling(DataGridView table, TableState tableState, ScaleState scaleState)
        {
            _table = table;
            _tableState = tableState;
            _scaleState = scaleState;
        }

        public void ApplyScaling(int diff)
        {
            if (_tableState.DataColumns.Count(c => c.Visible) == 1)
                ScaleOneDataColumn(diff);
            else if (_scaleState.FirstIsTarget)
                ScaleDataColumnsWithFirstVisibleAsTarget(diff);
            else
                ScaleDataColumns(diff);
        }

        private void ScaleDataColumns(int diff)
        {
            var width = _scaleState.TargetColumn.Width + diff;
            if (diff == 0 || width < 30)
                return;

            _tableState.ResizeController.BeginBulkColumnWidthChange();

            var scrollingOffset = _table.HorizontalScrollingOffset + diff * _scaleState.VisibleColumnsToLeft;

            for (int i = _tableState.DataColumnOffset; i < _table.ColumnCount; ++i)
            {
                if (i == _scaleState.FirstVisibleIndex || i == _tableState.PhantomColumnIndex) continue;
                _table.Columns[i].Width = width;
            }

            _table.Columns[_scaleState.FirstVisibleIndex].Width += diff;
            if (scrollingOffset < 0)
                _table.Columns[_scaleState.FirstVisibleIndex].Width += Math.Abs(scrollingOffset);

            var maxScrollingOffset = _tableState.ResizeController.GetTotalWidthInBulkColumnWidthChange() - _scaleState.TableDataAreaWidth;

            if (scrollingOffset > maxScrollingOffset)
                _table.Columns[_tableState.PhantomColumnIndex].Width += scrollingOffset - maxScrollingOffset;

            _scaleState.CurrentWidth = width;

            _tableState.ResizeController.CommitBulkColumnWidthChange(scrollingOffset);

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
            if (diff == 0 || (_table.Columns[_scaleState.FirstVisibleIndex].Width < 30) && diff < 0)
                return;
            _table.Columns[_scaleState.FirstVisibleIndex].Width += diff;
            var totalWidth = _tableState.ResizeController.GetTotalWidthInBulkColumnWidthChange();
            _table.Columns[_tableState.PhantomColumnIndex].Width += _scaleState.TableDataAreaWidth - totalWidth;
            _tableState.ResizeController.BeginBulkColumnWidthChange();
            for (int i = _tableState.DataColumnOffset; i < _table.ColumnCount; ++i)
            {
                if (i == _scaleState.FirstVisibleIndex || i == _tableState.PhantomColumnIndex) continue;
                _table.Columns[i].Width = _table.Columns[_scaleState.FirstVisibleIndex].Width;
            }
            _scaleState.CurrentWidth = _table.Columns[_scaleState.FirstVisibleIndex].Width;
            _tableState.ResizeController.CommitBulkColumnWidthChange();
        }

        private void ScaleDataColumnsWithFirstVisibleAsTarget(int diff)
        {
            var width = _scaleState.CurrentWidth + diff;
            if (diff == 0 || width < 30)
                return;
            if (_table.Columns[_scaleState.FirstVisibleIndex].Width <= _scaleState.CurrentWidth)
            {
                ScaleDataColumns(diff);
                return;
            }
            else
            {
                _table.Columns[_scaleState.FirstVisibleIndex].Width += diff;
                if (diff > 0)
                {
                    for (int i = _tableState.DataColumnOffset; i < _table.ColumnCount; ++i)
                    {
                        if (i == _scaleState.FirstVisibleIndex) continue;
                        _table.Columns[i].Width = width;
                    }
                    _scaleState.CurrentWidth = width;
                }
            }
        }

#if DEBUG
        private int DebugEdgePosition()
        {
            int pos = _table.Columns[_table.FirstDisplayedScrollingColumnIndex].Width - _table.FirstDisplayedScrollingColumnHiddenWidth;
            for (var i = _table.FirstDisplayedScrollingColumnIndex + 1; i < _scaleState.TargetColumn.Index; ++i)
                if (_table.Columns[i].Visible)
                    pos += _table.Columns[i].Width;
            return pos;
        }

        private int _debugEdge;

        public void SetDebugEdge()
        {
            _debugEdge = DebugEdgePosition();
        }
#endif
    }
}
