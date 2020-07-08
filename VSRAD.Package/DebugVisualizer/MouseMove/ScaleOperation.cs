using System;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove
{
    public sealed class ScaleOperation : IMouseMoveOperation
    {
        private readonly DataGridView _table;
        private TableState _tableState;

        private const int _maxDistanceFromDivider = 7;
        

        private bool _operationStarted;

        private int _orgScroll;
        private int _orgWidth;
        private int _orgMouseX;
        private int _orgNColumns; // number of visible columns before mouse cursor
        private int _orgSColumns; // number of fully scrolled columns
        private float _orgSPixels; // number of scrolled pixels in first displayed column

        public ScaleOperation(DataGridView table, TableState state)
        {
            _table = table;
            _tableState = state;
        }

        public bool OperationStarted() => _operationStarted;

        public bool AppliesOnMouseDown(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (!ShouldChangeCursor(hit, _tableState, e.X))
                return false;

            bool leftedge = (e.X - hit.ColumnX) < (_tableState.ColumnWidth/2);
            if (leftedge && hit.ColumnIndex == _tableState.GetFirstVisibleDataColumnIndex())
                return false;
            
            _operationStarted = false;
            _orgMouseX = Cursor.Position.X;
            _orgWidth = _tableState.ColumnWidth;
            _orgScroll = _tableState.GetCurrentScroll();
            _orgNColumns = _tableState.CountVisibleDataColumns(hit.ColumnIndex, !leftedge);
            _orgSColumns = _orgScroll / _orgWidth;
            _orgSPixels = _orgScroll % _orgWidth;

            return true;
        }

        public static bool ShouldChangeCursor(DataGridView.HitTestInfo hit, TableState state, int x)
        {
            if (hit.Type != DataGridViewHitTestType.ColumnHeader)
                return false;
            if (Math.Abs(x - hit.ColumnX) > _maxDistanceFromDivider && Math.Abs(x - hit.ColumnX - state.ColumnWidth) > _maxDistanceFromDivider)
                return false;

            return true;
        }

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != MouseButtons.Left)
                return false;

            var minWidth = _tableState.minAllowedWidth;

            int diff = Cursor.Position.X - _orgMouseX;
            if (_tableState.ScalingMode == ScalingMode.ResizeTable)
            {
                int orgL = _orgNColumns * _orgWidth - _orgScroll;
                int curL = orgL + diff;
                if (orgL > 0)
                {
                    float s = (float)curL / orgL;
                    int curWidth = (int)(s * _orgWidth);
                    curWidth = Math.Max(minWidth, curWidth);
                    s = (float)curWidth / _orgWidth;
                    int curScroll = _orgSColumns * curWidth + (int)(s * _orgSPixels);
                    _tableState.SetWidthAndScroll(curWidth, curScroll);
                }
            }
            else if (_tableState.ScalingMode == ScalingMode.ResizeColumn || _tableState.ScalingMode == ScalingMode.ResizeColumnAllowWide)
            {
                int orgL = _orgWidth;
                int curL = orgL + diff;
                float s = (float)curL / orgL;
                int curWidth = (int)(s * _orgWidth);
                curWidth = Math.Max(minWidth, curWidth);
                int curScroll = _orgScroll + (_orgNColumns - 1) * (curWidth - _orgWidth);
                _tableState.SetWidthAndScroll(curWidth, curScroll);
            }
            _operationStarted = true;
            return true;
        }
    }
}
