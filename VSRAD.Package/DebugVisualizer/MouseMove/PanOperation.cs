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
        private readonly DataGridView _table;
        private readonly TableState _state;

        private bool _thresholdReached;
        private int _lastX;
        private int _firstVisibleColumn;

        public PanOperation(DataGridView table, TableState state)
        {
            _table = table;
            _state = state;
        }

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (e.Button != MouseButtons.Left) return false;
            if (hit.ColumnIndex < VisualizerTable.DataColumnOffset || hit.RowIndex == -1) return false;

            _firstVisibleColumn = _table.Columns
                .Cast<DataGridViewTextBoxColumn>()
                .First(x => x.Visible == true && x.Index >= _state.DataColumnOffset)
                .Index;
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

                if (_table.Columns[_firstVisibleColumn].Width != _state.ColumnWidth 
                    && _table.Columns[_firstVisibleColumn].Displayed && diff > 0)
                {
                    var fistColumnWidth = _table.Columns[_firstVisibleColumn].Width - diff;
                    _table.Columns[_firstVisibleColumn].Width = Math.Max(fistColumnWidth, _state.ColumnWidth);
                }
                else if (_table.Columns[_state.PhantomColumnIndex].Width != 2
                    && _table.Columns[_state.PhantomColumnIndex].Displayed && diff < 0)
                {
                    var phantomWidth = _table.Columns[_state.PhantomColumnIndex].Width + diff;
                    _table.Columns[_state.PhantomColumnIndex].Width = Math.Max(phantomWidth, 2);
                }
                else
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
