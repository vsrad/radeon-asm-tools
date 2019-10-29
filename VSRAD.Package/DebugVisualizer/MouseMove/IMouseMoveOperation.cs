using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    public interface IMouseMoveOperation
    {
        bool HandleMouseMove(MouseEventArgs e);

        bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit);

        bool OperationStarted();
    }
}
