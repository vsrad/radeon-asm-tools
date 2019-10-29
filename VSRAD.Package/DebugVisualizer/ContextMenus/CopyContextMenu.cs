using System;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.ContextMenus
{
    public sealed class CopyContextMenu : IContextMenu
    {
        private readonly DataGridView _table;
        private readonly ContextMenu _menu;

        public CopyContextMenu(DataGridView table, Action processCopy)
        {
            _table = table;
            _menu = new ContextMenu(new[] { new MenuItem("Copy", (s, e) => processCopy()) });
        }

        public bool Show(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (hit.RowIndex == -1 || hit.ColumnIndex == -1) return false;
            if (!_table.Rows[hit.RowIndex].Cells[hit.ColumnIndex].Selected) return false;

            _menu.Show(_table, new Point(e.X, e.Y));
            return true;
        }
    }
}
