using System.Collections.Generic;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    public sealed class MouseMoveController
    {
        private readonly VisualizerTable _table;
        private readonly List<IMouseMoveOperation> _operations;
        private IMouseMoveOperation _currentOperation;

        private bool _operationAbortedOnMouseUp = false;

        public MouseMoveController(VisualizerTable table)
        {
            _table = table;
            _operations = new List<IMouseMoveOperation>
            {
                new ScaleOperation(_table),
                new PanOperation(_table),
                new ReorderOperation(_table)
            };
        }

        public bool HandleMouseDown(MouseEventArgs e)
        {
            var hit = _table.HitTest(e.X, e.Y);
            _currentOperation = _operations.Find(o => o.AppliesOnMouseDown(e, hit));

            if (_currentOperation == null) return false;

            _operationAbortedOnMouseUp = true;
            return true;
        }

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if (_currentOperation == null) return false;

            _operationAbortedOnMouseUp = !_currentOperation.OperationStarted();

            if (!_currentOperation.HandleMouseMove(e))
            {
                _currentOperation = null;
                return false;
            }
            return true;
        }

        public bool OperationDidNotFinishOnMouseUp()
        {
            var aborted = _operationAbortedOnMouseUp;
            _operationAbortedOnMouseUp = false;
            return aborted;
        }
    }
}