using System;
using System.IO;
using System.Windows.Forms;
using VSRAD.Package.Properties;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    public sealed class PanOperation : IMouseMoveOperation
    {
        private const int _thresholdX = 15;

        private readonly Cursor handCursor = new Cursor(new MemoryStream(Resources.HandCursor));
        private readonly VisualizerTable _table;

        private bool _thresholdReached;
        private int _lastX;

        public PanOperation(VisualizerTable table)
        {
            _table = table;
        }

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (e.Button != MouseButtons.Left) return false;
            if (hit.ColumnIndex < VisualizerTable.DataColumnOffset || hit.RowIndex == -1) return false;

            _lastX = Cursor.Position.X;
            _thresholdReached = false;
            return true;
        }

        public bool OperationStarted() => _thresholdReached;

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return false;

            Cursor.Current = handCursor;

            var x = Cursor.Position.X;
            if (_thresholdReached)
            {
                var diff = _lastX - x;
                _lastX = x;
                _table.HorizontalScrollingOffset = Math.Max(0, _table.HorizontalScrollingOffset + diff);
            }
            else if (Math.Abs(x - _lastX) > _thresholdX)
            {
                _thresholdReached = true;
            }

            return true;
        }
    }
}
