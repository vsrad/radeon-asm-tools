using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    sealed class ReorderOperation : IMouseMoveOperation
    {
        private readonly DataGridView _table;

        private bool _operationStarted;
        private int _hoverRowIndex;
        private int _newWatchRowIndex;
        private List<DataGridViewRow> _selectedRows;
        private List<DataGridViewRow> _rowsToMove;

        public ReorderOperation(DataGridView table)
        {
            _table = table;
        }

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit, bool _)
        {
            _newWatchRowIndex = _table.RowCount - 1;
            if (hit.Type != DataGridViewHitTestType.RowHeader
                || hit.RowIndex <= VisualizerTable.SystemRowIndex
                || hit.RowIndex == _newWatchRowIndex)
                return false;
            _operationStarted = false;
            _hoverRowIndex = hit.RowIndex;
            return true;
        }

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return false;

            if (!_operationStarted)
            {
                _selectedRows = _table.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Index != _newWatchRowIndex).ToList();
                if (_selectedRows.Contains(_table.Rows[_hoverRowIndex]))
                    _rowsToMove = _selectedRows.Where(r => r.Index != VisualizerTable.SystemRowIndex).ToList();
                else
                    _rowsToMove = new List<DataGridViewRow>() { _table.Rows[_hoverRowIndex] };
            }

            var nomalizedMouseX = Math.Min(Math.Max(e.X, 1), _table.Width - 2);
            var hit = _table.HitTest(nomalizedMouseX, e.Y);
            var indexDiff = hit.RowIndex - _hoverRowIndex;

            if (indexDiff != 0
                && _rowsToMove.Max(r => r.Index) + indexDiff < _newWatchRowIndex
                && _rowsToMove.Min(r => r.Index) + indexDiff > VisualizerTable.SystemRowIndex)
            {
                _operationStarted = true;

                _rowsToMove.Sort((r1, r2) => (indexDiff < 0) ? r1.Index.CompareTo(r2.Index) : r2.Index.CompareTo(r1.Index));

                // When SelectionMode is set to RowHeaderSelect and a selected row is removed,
                // DataGridView "compensates" for it by selecting an adjacent row.
                // As a result, rows that weren't a part of the selection at the start of the
                // reorder operation suddenly become selected, which is jarring.
                // To prevent this, we keep _selectedRows and restore them manually.
                var originalSelectionMode = _table.SelectionMode;
                _table.SelectionMode = DataGridViewSelectionMode.CellSelect;

                foreach (var row in _rowsToMove)
                {
                    var oldIndex = row.Index;
                    _table.Rows.Remove(row);
                    _table.Rows.Insert(oldIndex + indexDiff, row);
                }

                _table.SelectionMode = originalSelectionMode;
                foreach (var row in _selectedRows)
                    row.Selected = true;

                _hoverRowIndex = hit.RowIndex;
            }

            return true;
        }

        public bool HandleMouseWheel(MouseEventArgs _) => false;

        public bool OperationStarted() => _operationStarted;
    }
}
