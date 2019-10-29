using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    class ReorderOperation : IMouseMoveOperation
    {
        private readonly VisualizerTable _table;
        private bool _operationStarted;
        private List<DataGridViewRow> _selectedRows;
        private int _targetRowIndex;

        public ReorderOperation(VisualizerTable table)
        {
            _table = table;
            _operationStarted = false;
            _selectedRows = new List<DataGridViewRow>();
        }

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hti)
        {
            if (hti.RowIndex <= 0 
                || hti.RowIndex == _table.NewWatchRowIndex 
                || hti.Type != DataGridViewHitTestType.RowHeader)
                return false;
            _targetRowIndex = hti.RowIndex;
            return true;
        }

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                _operationStarted = false;
                _selectedRows.Clear();
                return false;
            }

            var nomalizedMouseX = Math.Min(Math.Max(e.X, 1), _table.Width - 2);

            var hti = _table.HitTest(nomalizedMouseX, e.Y);
            if (!_operationStarted)
            {
                if (_selectedRows.Count == 0)
                    SaveSelectedRowsBeforeReorder();
            }

            if (hti.RowIndex != _targetRowIndex && hti.RowIndex != _table.NewWatchRowIndex 
                && (hti.RowIndex != 0 && _table.ShowSystemRow) && hti.RowIndex != -1)
            {
                _operationStarted = true;
                var row = _table.Rows[_targetRowIndex];
                _table.Rows.RemoveAt(_targetRowIndex);
                _table.Rows.Insert(hti.RowIndex, row);
                RestoreSelectedRowsAfterReorder();
                _targetRowIndex = hti.RowIndex;
            }
            return true;
        }

        public bool OperationStarted() => _operationStarted;

        private void RestoreSelectedRowsAfterReorder()
        {
            foreach (var row in _selectedRows) row.Selected = true;
        }

        private void SaveSelectedRowsBeforeReorder()
        {
            _selectedRows = _table.SelectedRows.Cast<DataGridViewRow>().ToList();
        }
    }
}
