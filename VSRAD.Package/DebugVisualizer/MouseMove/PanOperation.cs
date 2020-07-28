using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VSRAD.Package.Properties;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    public sealed class PanOperation : IMouseMoveOperation
    {
        private const int _thresholdX = 15;

        private readonly Cursor handCursor = new Cursor(new MemoryStream(Resources.HandCursor));
        private readonly TableState _state;

        private bool _thresholdReached;
        private int _lastX;

        public PanOperation(TableState state)
        {
            _state = state;
        }

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (e.Button != MouseButtons.Left) return false;
            if (_state.ScalingMode == ScalingMode.ResizeQuad)
            {
                float f = _state.GetNormalizedXCoordinate(e.X);
                if (f <= 0.25 || f >= 0.75)
                    return false;
            }
            else
            {
                if (hit.RowIndex == -1) return false;
                if (hit.ColumnIndex < VisualizerTable.DataColumnOffset && hit.ColumnIndex != VisualizerTable.PhantomColumnIndex) return false;
            }

            _lastX = Cursor.Position.X;
            _thresholdReached = false;
            return true;
        }

        public bool OperationStarted() => _thresholdReached;

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return false;

            Cursor.Current = handCursor;

            var x = Cursor.Position.X;
            if (_thresholdReached)
            {
                var diff = _lastX - x;
                _lastX = x;

                _state.Scroll(diff, true);
            }
            else if (Math.Abs(x - _lastX) > _thresholdX)
            {
                _thresholdReached = true;
            }

            return true;
        }
    }
}
