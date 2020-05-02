using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class CustomTableGraphics
    {
        private readonly VisualizerTable _table;
        private readonly IFontAndColorProvider _fontAndColor;
        private static readonly Brush _avgprColor = Brushes.LightGreen;

        public CustomTableGraphics(VisualizerTable table, IFontAndColorProvider fontAndColor)
        {
            _table = table;
            _table.CellPainting += PaintSpacesInVisibility;
            _table.CellPainting += PaintInvalidWatchName;
            _table.RowPostPaint += ReplaceDefaultRowHeaderBitmap;

            _fontAndColor = fontAndColor;
        }

        private void PaintSpacesInVisibility(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < VisualizerTable.DataColumnOffset || e.ColumnIndex == VisualizerTable.DataColumnCount) return;

            int width;
            SolidBrush color;

            if (_table.Columns[e.ColumnIndex].Visible != _table.Columns[e.ColumnIndex + 1].Visible)
            {
                width = _table.HiddenColumnSeparatorWidth;
                color = _fontAndColor.FontAndColorState.HiddenColumnSeparatorBrush;
            }
            else if (_table.LaneGrouping != 0 && (e.ColumnIndex % _table.LaneGrouping == 0))
            {
                width = _table.LaneSeparatorWidth;
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

        private void PaintInvalidWatchName(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex == -1 || e.Value == null) return;
            if (e.Value.ToString().IndexOf(':') >= 0)
                e.CellStyle.BackColor = Color.Red;
        }

        // Show variable type in row header and highlight AVGPR watches
        private void ReplaceDefaultRowHeaderBitmap(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (e.RowIndex == _table.NewWatchRowIndex) return;

            var selectedWatch = VisualizerTable.GetRowWatchState(_table.Rows[e.RowIndex]);
            if (selectedWatch.IsAVGPR)
            {
                var highlightedRect = new Rectangle(e.RowBounds.Left + 3, e.RowBounds.Top + 3, _table.RowHeadersWidth - 6, e.RowBounds.Height - 6);
                e.Graphics.FillRectangle(_avgprColor, highlightedRect);
            }

            e.PaintHeader(
                    DataGridViewPaintParts.Background
                    | DataGridViewPaintParts.Border
                    | DataGridViewPaintParts.Focus
                    | DataGridViewPaintParts.SelectionBackground
                    | DataGridViewPaintParts.ContentForeground
                );

            if (!selectedWatch.IsEmpty)
            {
                var typeTextPos = new PointF((float)e.RowBounds.Left + 7, (float)e.RowBounds.Top + 4);
                e.Graphics.DrawString(selectedWatch.Type.ShortName(),
                    _table.RowHeadersDefaultCellStyle.Font,
                    new SolidBrush(_table.RowHeadersDefaultCellStyle.ForeColor),
                    typeTextPos);
            }
        }
    }
}
