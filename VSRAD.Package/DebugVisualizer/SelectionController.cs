using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Input;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class SelectionController
    {
        private readonly DataGridView _table;

        public SelectionController(DataGridView table)
        {
            _table = table;
        }

        public IEnumerable<DataGridViewRow> GetSelectedRows()
        {
            foreach (DataGridViewRow row in _table.Rows)
                if (row.Index >= 0 && row.Index != _table.RowCount - 1 && row.Cells[VisualizerTable.NameColumnIndex].Selected)
                    yield return row;
        }

        public IEnumerable<DataGridViewRow> GetClickTargetRows(int clickedRowIndex)
        {
            if (_table.Rows[clickedRowIndex].Cells[VisualizerTable.NameColumnIndex].Selected)
                return GetSelectedRows();
            else
                return new[] { _table.Rows[clickedRowIndex] };
        }

        public bool SelectAllColumnsInRange(int fromIndex, int toIndex)
        {
            toIndex = Math.Min(toIndex, _table.Columns.Count - 1);

            bool anySelected = false;

            _table.ClearSelection();
            for (int i = fromIndex; i <= toIndex; ++i)
            {
                if (_table.Columns[i].Visible)
                {
                    if (!anySelected)
                        _table.FirstDisplayedScrollingColumnIndex = i;

                    anySelected = true;
                    foreach (DataGridViewRow row in _table.Rows)
                        if (row.Index >= 0 && row.Index != _table.RowCount - 1)
                            row.Cells[i].Selected = true;
                }
            }

            return anySelected;
        }

        // We want to select a column when the column header is clicked, and a row when the row header is clicked.
        // The table can select _either_ rows or columns, but not both, so we manually change selection mode.
        public void SwitchMode(DataGridViewSelectionMode newMode)
        {
            if (_table.SelectionMode == newMode) return;

            // When holding Shift or Ctrl, the user expects to append to the current selection,
            // but changing the mode clears it, so we need to manually preserve it.
            var selectedCells = _table.SelectedCells;
            _table.SelectionMode = newMode;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.LeftCtrl))
                foreach (DataGridViewCell cell in selectedCells)
                    cell.Selected = true;
        }
    }
}
