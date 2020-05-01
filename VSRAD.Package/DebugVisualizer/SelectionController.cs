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
