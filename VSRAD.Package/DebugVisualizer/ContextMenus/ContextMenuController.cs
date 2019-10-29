using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.ContextMenus
{
    public sealed class ContextMenuController
    {
        private readonly VisualizerTable _table;
        private readonly IContextMenu[] _contextMenus;

        public ContextMenuController(VisualizerTable table, IContextMenu[] menus)
        {
            _table = table;
            _contextMenus = menus;
            _table.MouseClick += HandleMouseClick;
        }

        private void HandleMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _table.HitTest(e.X, e.Y);
            foreach (var menu in _contextMenus)
                if (menu.Show(e, hit)) break;
        }
    }
}
