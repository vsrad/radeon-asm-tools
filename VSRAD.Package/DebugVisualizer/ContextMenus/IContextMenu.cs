using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.ContextMenus
{
    public interface IContextMenu
    {
        bool Show(MouseEventArgs e, DataGridView.HitTestInfo hit);
    }
}
