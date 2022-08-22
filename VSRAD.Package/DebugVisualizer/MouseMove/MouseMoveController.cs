using System.Collections.Generic;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    public sealed class MouseMoveController
    {
        private readonly DataGridView _table;
        private readonly TableState _state;
        private readonly List<IMouseMoveOperation> _operations;
        private IMouseMoveOperation _currentOperation;

        private bool _operationAbortedOnMouseUp = false;

        public MouseMoveController(DataGridView table, TableState state)
        {
            _table = table;
            _state = state;
            _operations = new List<IMouseMoveOperation>
            {
                new ScaleOperation(_table, _state),
                new PanOperation(_state),
                new ReorderOperation(_table)
            };
            _table.MouseWheel += (s, e) => HandleMouseWheel(e);
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

        public bool HandleMouseWheel(MouseEventArgs e)
        {
            foreach (var op in _operations)
                if (op.HandleMouseWheel(e)) return true;

            return false;
        }

        public bool OperationDidNotFinishOnMouseUp()
        {
            var aborted = _operationAbortedOnMouseUp;
            _operationAbortedOnMouseUp = false;
            return aborted;
        }
    }
}