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
        private int _orgeX;
        private int _orgNColumns; // number of visible columns before mouse cursor
        private int _orgSColumns; // number of fully scrolled columns
        private bool _lefthalf; // mouse is in left half of the data columns region
        private float _orgSPixels; // number of scrolled pixels in first displayed column

        private bool _isNameColumn;

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

            _isNameColumn = (leftedge && hit.ColumnIndex == _tableState.GetFirstVisibleDataColumnIndex())
                || hit.ColumnIndex < _tableState.GetFirstVisibleDataColumnIndex();
            _operationStarted = false;
            _orgMouseX = Cursor.Position.X;
            _orgeX = e.X;
            _orgWidth = _isNameColumn && _tableState.NameColumnScalingEnabled
                            ? _table.Columns[_tableState.NameColumnIndex].Width
                            : _tableState.ColumnWidth;
            _orgScroll = _tableState.GetCurrentScroll();
            _orgNColumns = _tableState.CountVisibleDataColumns(hit.ColumnIndex, !leftedge);
            _orgSColumns = _orgScroll / _orgWidth;
            _orgSPixels = _orgScroll % _orgWidth;
            _lefthalf = _tableState.GetNormalizedXCoordinate(e.X) < 0.5;

            return true;
        }

        public static bool ShouldChangeCursor(DataGridView.HitTestInfo hit, TableState state, int x)
        {
            // match the right edge of the name column regardless of its position if name column scaling is enabled
            if ((Math.Abs(x - state.NameColumnEdge) < _maxDistanceFromDivider) && state.NameColumnScalingEnabled)
                return true;

            if (state.ScalingMode == ScalingMode.ResizeQuad)
            {
                float f = state.GetNormalizedXCoordinate(x);

                if (!((f > 0 && f < 0.25) || (f > 0.75 && f < 1)))
                    return false;

                if (hit.Type != DataGridViewHitTestType.ColumnHeader && hit.Type != DataGridViewHitTestType.Cell)
                    return false;
            }
            else if (state.ScalingMode == ScalingMode.ResizeHalf)
            {
                if (hit.Type != DataGridViewHitTestType.ColumnHeader)
                    return false;

                float f = state.GetNormalizedXCoordinate(x);
                if (!(f > 0 && f < 1))
                    return false;
            }
            else
            {
                if (hit.Type != DataGridViewHitTestType.ColumnHeader)
                    return false;
                if (Math.Abs(x - hit.ColumnX) > _maxDistanceFromDivider && Math.Abs(x - hit.ColumnX - state.ColumnWidth) > _maxDistanceFromDivider)
                    return false;
            }

            return true;
        }

        public bool HandleMouseMove(MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != MouseButtons.Left)
                return false;

            var minWidth = _tableState.minAllowedWidth;

            int diff = Cursor.Position.X - _orgMouseX;
            if (_tableState.ScalingMode == ScalingMode.ResizeTable
                || (_tableState.ScalingMode == ScalingMode.ResizeQuad && !_lefthalf)
                || (_tableState.ScalingMode == ScalingMode.ResizeHalf && !_lefthalf))
            {
                int orgL = _isNameColumn ? _orgWidth
                                         : _orgNColumns * _orgWidth - _orgScroll;
                int curL = orgL + diff;
                if (orgL > 0)
                {
                    float s = (float)curL / orgL;
                    int curWidth = (int)(s * _orgWidth);
                    curWidth = Math.Max(minWidth, curWidth);
                    s = (float)curWidth / _orgWidth;
                    int curScroll = _orgSColumns * curWidth + (int)(s * _orgSPixels);
                    if (_isNameColumn && _tableState.NameColumnScalingEnabled)
                        _tableState.ScaleNameColumn(curWidth);
                    else
                        _tableState.SetWidthAndScroll(curWidth, curScroll);
                }
            }
            else if ((_tableState.ScalingMode == ScalingMode.ResizeQuad && _lefthalf)
                || (_tableState.ScalingMode == ScalingMode.ResizeHalf && _lefthalf))
            {
                int orgL = _isNameColumn ? _orgWidth
                                         : _tableState.Table.Width - _orgeX;
                int curL = _isNameColumn ? orgL + diff : orgL - diff;
                if (orgL > 0)
                {
                    float s = (float)curL / orgL;
                    int curWidth = (int)(s * _orgWidth);
                    curWidth = Math.Max(minWidth, curWidth);
                    s = (float)curWidth / _orgWidth;
                    int curScroll = (int)(_orgScroll * s + _tableState.GetDataRegionWidth() * (s - 1));
                    if (_isNameColumn && _tableState.NameColumnScalingEnabled)
                        _tableState.ScaleNameColumn(curWidth);
                    else
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
                if (_isNameColumn && _tableState.NameColumnScalingEnabled)
                    _tableState.ScaleNameColumn(curWidth);
                else
                    _tableState.SetWidthAndScroll(curWidth, curScroll);
            }
            _operationStarted = true;
            return true;
        }
    }
}
