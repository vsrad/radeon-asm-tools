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
        public delegate void AddWatchRange(string name, int from, int to);

        private readonly VisualizerTable _table;
        private readonly ContextMenu _menu;
        private readonly MenuItem _avgprButton;
        private int _currentRow;

        public TypeContextMenu(VisualizerTable table, TypeChanged typeChanged, AVGPRStateChanged avgprChanged, Action processCopy,
            InsertRow insertRow, AddWatchRange addWatchRange)
        {
            _table = table;

            var typeItems = new MenuItem[]
            {
                new MenuItem("Hex",    (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Hex,   32))),
                new MenuItem("Float",  (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Float, 32))),
                new MenuItem("Half",   (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Float, 16))),
                new MenuItem("Int32",  (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Int,   32))),
                new MenuItem("UInt32", (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Uint,  32))),
                new MenuItem("Other", new MenuItem[]
                {
                    new MenuItem("Int16",  (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Int,  16))),
                    new MenuItem("Int8",   (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Int,   8))),
                    new MenuItem("UInt16", (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Uint, 16))),
                    new MenuItem("Uint8",  (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Uint,  8))),
                    new MenuItem("Bin",    (s, e) => typeChanged(_currentRow, new VariableType(VariableCategory.Bin,  32)))
                })
            };

            var fgColor = new MenuItem("Font Color", new[]
            {
                new MenuItem("Green", (s, e) => _table.ApplyRowHighlight(_currentRow, changeFg: DataHighlightColor.Green)),
                new MenuItem("Red", (s, e) => _table.ApplyRowHighlight(_currentRow, changeFg: DataHighlightColor.Red)),
                new MenuItem("Blue", (s, e) => _table.ApplyRowHighlight(_currentRow, changeFg: DataHighlightColor.Blue)),
                new MenuItem("None", (s, e) => _table.ApplyRowHighlight(_currentRow, changeFg: DataHighlightColor.None))
            });
            var bgColor = new MenuItem("Background Color", new[]
            {
                new MenuItem("Green", (s, e) => _table.ApplyRowHighlight(_currentRow, changeBg: DataHighlightColor.Green)),
                new MenuItem("Red", (s, e) => _table.ApplyRowHighlight(_currentRow, changeBg: DataHighlightColor.Red)),
                new MenuItem("Blue", (s, e) => _table.ApplyRowHighlight(_currentRow, changeBg: DataHighlightColor.Blue)),
                new MenuItem("None", (s, e) => _table.ApplyRowHighlight(_currentRow, changeBg: DataHighlightColor.None))
            });

            var insertRowBefore = new MenuItem("Insert Row Before", (s, e) => insertRow(_currentRow, false));
            var insertRowAfter = new MenuItem("Insert Row After", (s, e) => insertRow(_currentRow, true));

            _avgprButton = new MenuItem("AVGPR", (s, e) =>
            {
                _avgprButton.Checked = !_avgprButton.Checked;
                avgprChanged(_currentRow, _avgprButton.Checked);
            });

            var copy = new MenuItem("Copy", (s, e) => processCopy());

            var addToWatchesAsArray = new MenuItem("Add to watches as array", Enumerable.Range(0, 16)
                .Select(i =>
                    new MenuItem(i.ToString(), Enumerable.Range(i, 16 - i).Select(y => new MenuItem(y.ToString(),
                    (s, e) =>
                    {
                        var watchName = VisualizerTable.GetRowWatchState(_table.Rows[_currentRow]).Name;
                        addWatchRange(watchName, i, y);
                    })).Prepend(new MenuItem("To") { Enabled = false }).ToArray())
                ).Prepend(new MenuItem("From") { Enabled = false }).ToArray());

            var menuItems = typeItems.Concat(new[]
            {
                new MenuItem("-"),
                fgColor,
                bgColor,
                new MenuItem("-"),
                copy,
                new MenuItem("-"),
                insertRowBefore,
                insertRowAfter,
                new MenuItem("-"),
                addToWatchesAsArray
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
            _avgprButton.Enabled = _currentRow != 0 || !_table.ShowSystemRow;
            _avgprButton.Checked = selectedWatch.IsAVGPR;

            _menu.Show(_table, new Point(e.X, e.Y));
            return true;
        }
    }
}
