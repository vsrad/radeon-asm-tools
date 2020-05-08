using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.ContextMenus
{
    public sealed class TypeContextMenu : IContextMenu
    {
        public delegate void TypeChanged(int rowIndex, VariableType type);
        public delegate void AVGPRStateChanged(int rowIndex, bool state);
        public delegate void InsertRow(int rowIndex, bool after);

        private readonly VisualizerTable _table;
        private readonly ContextMenu _menu;
        private readonly MenuItem _avgprButton;
        private int _currentRow;

        public TypeContextMenu(VisualizerTable table, TypeChanged typeChanged, AVGPRStateChanged avgprChanged, Action processCopy, InsertRow insertRow)
        {
            _table = table;

            var typeItems = ((VariableType[])Enum.GetValues(typeof(VariableType)))
                .Select(type => new MenuItem(type.ToString(), (s, e) => typeChanged(_currentRow, type)));

            var fgColor = new MenuItem("Font Color", new[]
            {
                new MenuItem("Green", (s, e) => _table.ApplyRowHighlight(_currentRow, DataHighlightColor.Green, fg: true)),
                new MenuItem("Red", (s, e) => _table.ApplyRowHighlight(_currentRow, DataHighlightColor.Red, fg: true)),
                new MenuItem("Blue", (s, e) => _table.ApplyRowHighlight(_currentRow, DataHighlightColor.Blue, fg: true)),
                new MenuItem("None", (s, e) => _table.ApplyRowHighlight(_currentRow, DataHighlightColor.None, fg: true))
            });
            var bgColor = new MenuItem("Background Color", new[]
            {
                new MenuItem("Green", (s, e) => _table.ApplyRowHighlight(_currentRow, DataHighlightColor.Green, fg: false)),
                new MenuItem("Red", (s, e) => _table.ApplyRowHighlight(_currentRow, DataHighlightColor.Red, fg: false)),
                new MenuItem("Blue", (s, e) => _table.ApplyRowHighlight(_currentRow, DataHighlightColor.Blue, fg: false)),
                new MenuItem("None", (s, e) => _table.ApplyRowHighlight(_currentRow, DataHighlightColor.None, fg: false))
            });

            var insertRowBefore = new MenuItem("Insert Row Before", (s, e) => insertRow(_currentRow, false));
            var insertRowAfter = new MenuItem("Insert Row After", (s, e) => insertRow(_currentRow, true));

            _avgprButton = new MenuItem("AVGPR", (s, e) =>
            {
                _avgprButton.Checked = !_avgprButton.Checked;
                avgprChanged(_currentRow, _avgprButton.Checked);
            });

            var copy = new MenuItem("Copy", (s, e) => processCopy());

            var menuItems = typeItems.Concat(new[]
            {
                new MenuItem("-"),
                fgColor,
                bgColor,
                new MenuItem("-"),
                copy,
                new MenuItem("-"),
                insertRowBefore,
                insertRowAfter
                //_avgprButton
            });

            _menu = new ContextMenu(menuItems.ToArray());
        }

        public bool Show(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (hit.RowIndex == _table.NewWatchRowIndex || hit.RowIndex == -1) return false;
            if (hit.ColumnIndex != VisualizerTable.NameColumnIndex && hit.ColumnIndex != -1) return false;

            _currentRow = hit.RowIndex;

            foreach (MenuItem item in _menu.MenuItems)
                item.Checked = false;

            var selectedWatch = VisualizerTable.GetRowWatchState(_table.Rows[hit.RowIndex]);
            _menu.MenuItems[(int)selectedWatch.Type].Checked = true;
            _avgprButton.Enabled = _currentRow != 0 || !_table.ShowSystemRow;
            _avgprButton.Checked = selectedWatch.IsAVGPR;

            _menu.Show(_table, new Point(e.X, e.Y));
            return true;
        }
    }
}
