using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class SelectionController
    {
        public IEnumerable<int> SelectedWatchIndexes => _table.SelectedCells
            .Cast<DataGridViewCell>()
            .Where(x => x.ColumnIndex == VisualizerTable.NameColumnIndex && x.Value != null)
            .Select(x => x.RowIndex)
            .OrderByDescending(x => x);

        private readonly DataGridView _table;

        public SelectionController(DataGridView table)
        {
            _table = table;
        }

        #region Table headers

        // We want to select a column when the column header is clicked, and a row when the row header is clicked.
        // The table can select _either_ rows or columns, but not both, so we manually change selection mode.

        public void ColumnHeaderClicked(int columnIndex)
        {
            if (_table.SelectionMode == DataGridViewSelectionMode.ColumnHeaderSelect) return;

            // When holding shift or ctrl, the user expects to append to the selection. However, changing
            // selection mode clears the current selection, so we need to manually preserve it.
            var selectedCells = _table.SelectedCells;
            _table.SelectionMode = DataGridViewSelectionMode.ColumnHeaderSelect;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.LeftCtrl))
                foreach (DataGridViewCell cell in selectedCells)
                    cell.Selected = true;

            // If we've just changed selection mode, we need to manually select the column
            _table.Columns[columnIndex].Selected = true;
        }

        public void RowHeaderClicked(int rowIndex)
        {
            if (_table.SelectionMode == DataGridViewSelectionMode.RowHeaderSelect) return;

            var selectedCells = _table.SelectedCells;
            _table.SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.LeftCtrl))
                foreach (DataGridViewCell cell in selectedCells)
                    cell.Selected = true;

            _table.Rows[rowIndex].Selected = true;
        }

        #endregion
    }
}
