using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    sealed class ReorderOperation : IMouseMoveOperation
    {
        private readonly VisualizerTable _table;

        private bool _operationStarted;
        private DataGridViewRow _mouseDownRow;
        private List<DataGridViewRow> _userWatchRows;
        private List<DataGridViewRow> _selectedRows;
        private List<DataGridViewRow> _rowsToMove;

        public ReorderOperation(VisualizerTable table)
        {
            _table = table;
        }

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (hit.Type != DataGridViewHitTestType.RowHeader || hit.RowIndex < 0)
                return false;

            _mouseDownRow = _table.Rows[hit.RowIndex];
            if (!_table.IsUserWatchRow(_mouseDownRow))
                return false;

            _operationStarted = false;
            _userWatchRows = _table.GetUserWatchRows().ToList();
            return true;
        }

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return false;

            if (!_operationStarted)
            {
                _selectedRows = _table.GetSelectedUserWatchRows().ToList();
                var userRowsToMove = _selectedRows.Contains(_mouseDownRow) ? _selectedRows : (IEnumerable<DataGridViewRow>)new[] { _mouseDownRow };
                _rowsToMove = _table.Rows.Cast<DataGridViewRow>()
                    .Where(r => userRowsToMove.Contains(r) || userRowsToMove.Contains(((WatchNameCell)r.Cells[VisualizerTable.NameColumnIndex]).ParentRows.FirstOrDefault())).ToList();
            }

            var nomalizedMouseX = Math.Min(Math.Max(e.X, 1), _table.Width - 2);
            var hit = _table.HitTest(nomalizedMouseX, e.Y);
            if (hit.RowIndex >= 0)
            {
                var hoverRow = _table.Rows[hit.RowIndex];
                var hoverWatchIndex = _userWatchRows.IndexOf(hoverRow);
                if (hoverWatchIndex != -1 && hoverWatchIndex != _userWatchRows.IndexOf(_mouseDownRow))
                {
                    _operationStarted = true;

                    int moveToRowIndex;
                    if (hoverRow.Index > _mouseDownRow.Index)
                        moveToRowIndex = (hoverWatchIndex + 1 < _userWatchRows.Count ? _userWatchRows[hoverWatchIndex + 1].Index : _table.NewWatchRowIndex) - 1;
                    else
                        moveToRowIndex = hoverRow.Index;

                    _rowsToMove.Sort((r1, r2) => hoverRow.Index > _mouseDownRow.Index ? r1.Index.CompareTo(r2.Index) : r2.Index.CompareTo(r1.Index));

                    // When SelectionMode is set to RowHeaderSelect and a selected row is removed,
                    // DataGridView "compensates" for it by selecting an adjacent row.
                    // As a result, rows that weren't a part of the selection at the start of the
                    // reorder operation suddenly become selected, which is jarring.
                    // To prevent this, we keep _selectedRows and restore them manually.
                    var originalSelectionMode = _table.SelectionMode;
                    _table.SelectionMode = DataGridViewSelectionMode.CellSelect;

                    foreach (var row in _rowsToMove)
                    {
                        //var oldIndex = row.Index;
                        _table.Rows.Remove(row);
                        _table.Rows.Insert(moveToRowIndex, row);
                    }

                    _table.SelectionMode = originalSelectionMode;
                    foreach (var row in _selectedRows)
                        row.Selected = true;

                    // Rearrange user watch indexes according to new row positions
                    _userWatchRows.Sort((a, b) => a.Index.CompareTo(b.Index));
                }
            }

            return true;
        }

        public bool OperationStarted() => _operationStarted;
    }
}
