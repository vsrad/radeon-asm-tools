using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.ContextMenus
{
    public sealed class TypeContextMenu : IContextMenu
    {
        public delegate void TypeChanged(int rowIndex, VariableInfo type);
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

            //var typeItems = Array.Empty<MenuItem>(); // TODO: manually add corresponding values

            //var typeItems = ((VariableType[])Enum.GetValues(typeof(VariableType)))
            //    .Select(type => new MenuItem(type.ToString(), (s, e) => typeChanged(_currentRow, type)));

            var typeItems = new MenuItem[]
            {
                new MenuItem("Hex", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Hex, Size = 32 })),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Hex, Size = 16 })),
                    new MenuItem("8" , (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Hex, Size = 8  }))
                }),
                new MenuItem("Int", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Int, Size = 32 })),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Int, Size = 16 })),
                    new MenuItem("8" , (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Int, Size = 8  }))
                }),
                new MenuItem("UInt", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Uint, Size = 32 })),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Uint, Size = 16 })),
                    new MenuItem("8" , (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Uint, Size = 8  }))
                }),
                new MenuItem("Float", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Float, Size = 32 })),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Float, Size = 16 }))
                }),
                new MenuItem("Bin", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Float, Size = 32 })),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Float, Size = 16 })),
                    new MenuItem("8" , (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Float, Size = 8  }))
                }),
                new MenuItem("Half", (s, e) => typeChanged(_currentRow, new VariableInfo { Type = VariableType.Half, Size = 0 }))
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
            //_menu.MenuItems[(int)selectedWatch.Info.Type].Checked = true;
            _avgprButton.Enabled = _currentRow != 0 || !_table.ShowSystemRow;
            _avgprButton.Checked = selectedWatch.IsAVGPR;

            _menu.Show(_table, new Point(e.X, e.Y));
            return true;
        }
    }
}
