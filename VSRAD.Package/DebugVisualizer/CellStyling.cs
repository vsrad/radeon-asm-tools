using System.Drawing;
using System.Windows.Forms;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;
using static VSRAD.Package.Utils.NativeMethods;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class CellStyling
    {
        private readonly VisualizerTable _table;
        private readonly VisualizerAppearance _appearance;
        private readonly ComputedColumnStyling _computedStyling;
        private readonly IFontAndColorProvider _fontAndColor;
        private readonly SolidBrush _tableBackgroundBrush;

        public CellStyling(VisualizerTable table, VisualizerAppearance appearance, ComputedColumnStyling computedStyling, IFontAndColorProvider fontAndColor)
        {
            _table = table;
            _appearance = appearance;
            _computedStyling = computedStyling;
            _fontAndColor = fontAndColor;
            _tableBackgroundBrush = new SolidBrush(_table.BackgroundColor);

            _table.CellPainting += HandleCellPaint;
        }

        private void HandleCellPaint(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var dataColumnIndex = e.ColumnIndex - VisualizerTable.DataColumnOffset;
            var isDataColumn = dataColumnIndex >= 0 && dataColumnIndex < _table.DataColumnCount;
            var isPhantomColumn = e.ColumnIndex == VisualizerTable.PhantomColumnIndex;
            var isDataRow = e.RowIndex >= 0;

            if (isPhantomColumn)
                PaintBackground(e);
            if (isDataRow)
                PaintInvalidWatchName(e);
            if (isDataColumn && isDataRow)
                GrayOutInactiveLanes(dataColumnIndex, e);
            if (isDataRow)
                DarkenAlternatingRows(e);
            if (isDataColumn)
                PaintColumnSeparators(dataColumnIndex, e);
        }

        private void PaintBackground(DataGridViewCellPaintingEventArgs e)
        {
            e.Graphics.FillRectangle(_tableBackgroundBrush, e.CellBounds);
            e.Handled = true;
        }

        private void DarkenAlternatingRows(DataGridViewCellPaintingEventArgs e)
        {
            if (_appearance.DarkenAlternatingRowsBy == 0 || e.RowIndex % 2 == 0 || e.RowIndex == _table.NewWatchRowIndex)
                return;

            e.CellStyle.ForeColor = DarkenColor(e.CellStyle.ForeColor, _appearance.DarkenAlternatingRowsBy / 100f);
            e.CellStyle.BackColor = DarkenColor(e.CellStyle.BackColor, _appearance.DarkenAlternatingRowsBy / 100f);
        }

        private void PaintInvalidWatchName(DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex == VisualizerTable.NameColumnIndex && e.Value is string watchName && !string.IsNullOrWhiteSpace(watchName) && !Watch.IsWatchNameValid(watchName))
                e.CellStyle.BackColor = Color.Red;
        }

        private void GrayOutInactiveLanes(int dataColumnIndex, DataGridViewCellPaintingEventArgs e)
        {
            if (!_table.WatchDataValid || (_computedStyling.ColumnState[dataColumnIndex] & ColumnStates.Inactive) != 0 || (e.Value is string v && v.Length == 0))
            {
                e.CellStyle.ForeColor = _fontAndColor.FontAndColorState.HighlightForeground[(int)DataHighlightColor.None];
                e.CellStyle.BackColor = _fontAndColor.FontAndColorState.HighlightBackground[(int)DataHighlightColor.Inactive];
            }
        }

        private void PaintColumnSeparators(int dataColumnIndex, DataGridViewCellPaintingEventArgs e)
        {
            int width;
            SolidBrush color;

            if ((_computedStyling.ColumnState[dataColumnIndex] & ColumnStates.HasHiddenColumnSeparator) != 0)
            {
                width = _appearance.HiddenColumnSeparatorWidth;
                color = _fontAndColor.FontAndColorState.HiddenColumnSeparatorBrush;
            }
            else if ((_computedStyling.ColumnState[dataColumnIndex] & ColumnStates.HasLaneSeparator) != 0)
            {
                width = _appearance.LaneSeparatorWidth;
                color = _fontAndColor.FontAndColorState.ColumnSeparatorBrush;
            }
            else return;

            // We doing force paint of _visible_ part of cell.
            // Since we have frozen columns visible part of cell
            // is not necessarily whole cell.
            var r = e.CellBounds.Left > _table.ReservedColumnsOffset
                ? e.CellBounds
                : new Rectangle(_table.ReservedColumnsOffset + 1, e.CellBounds.Top, e.CellBounds.Right - _table.ReservedColumnsOffset - 1, e.CellBounds.Height);
            r.Width -= width;
            e.Graphics.SetClip(r);
            e.Paint(r, DataGridViewPaintParts.All);
            e.Graphics.SetClip(e.CellBounds);
            r = new Rectangle(r.Right - 1, r.Top, width + 1, r.Height);
            e.Graphics.FillRectangle(color, r);
            e.Graphics.ResetClip();
            e.Handled = true;
        }

        private static Color DarkenColor(Color c, float by)
        {
            ushort h = 0, l = 0, s = 0;
            c.ToHls(ref h, ref l, ref s);
            l = (ushort)(l * (1 - by));
            return FromHls(h, l, s);
        }
    }
}
