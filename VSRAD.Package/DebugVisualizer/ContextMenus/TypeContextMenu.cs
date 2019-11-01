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
        public delegate void FontColorChanged(int rowIndex, Color color);

        private readonly VisualizerTable _table;
        private readonly ContextMenu _menu;
        private readonly MenuItem _avgprButton;
        private int _currentRow;

        public TypeContextMenu(VisualizerTable table, TypeChanged typeChanged, AVGPRStateChanged avgprChanged, FontColorChanged fontColorChanged, Action processCopy)
        {
            _table = table;

            var typeItems = ((VariableType[])Enum.GetValues(typeof(VariableType)))
                .Select(type => new MenuItem(type.ToString(), (s, e) => typeChanged(_currentRow, type)));

            var fontColorSubmenu = new MenuItem("Font Color", new[]
            {
                new MenuItem("Green", (s, e) => fontColorChanged(_currentRow, Color.Green)),
                new MenuItem("Red", (s, e) => fontColorChanged(_currentRow, Color.Red)),
                new MenuItem("Blue", (s, e) => fontColorChanged(_currentRow, Color.Blue)),
                new MenuItem("Default", (s, e) => fontColorChanged(_currentRow, Color.Empty))
            });

            _avgprButton = new MenuItem("AVGPR", (s, e) =>
            {
                _avgprButton.Checked = !_avgprButton.Checked;
                avgprChanged(_currentRow, _avgprButton.Checked);
            });

            var copy = new MenuItem("Copy", (s, e) => processCopy());

            var menuItems = typeItems.Concat(new[]
            {
                new MenuItem("-"),
                fontColorSubmenu,
                new MenuItem("-"),
                copy
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

            var selectedWatch = _table.GetRowWatchState(_table.Rows[hit.RowIndex]);
            _menu.MenuItems[(int)selectedWatch.Type].Checked = true;
            _avgprButton.Enabled = _currentRow != 0 || !_table.ShowSystemRow;
            _avgprButton.Checked = selectedWatch.IsAVGPR;

            _menu.Show(_table, new Point(e.X, e.Y));
            return true;
        }
    }
}
