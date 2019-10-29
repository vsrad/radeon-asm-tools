using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class CustomTableGraphics
    {
        private readonly VisualizerTable _table;
        private const int _visibilitySpaceWidth = 8;
        private static readonly Brush _visibilitySpaceColor = Brushes.Black;
        private static readonly Brush _avgprColor = Brushes.LightGreen;

        public CustomTableGraphics(VisualizerTable table)
        {
            _table = table;
            _table.CellPainting += PaintSpacesInVisibility;
            _table.CellPainting += PaintInvalidWatchName;
            _table.RowPostPaint += ReplaceDefaultRowHeaderBitmap;
        }

        private void PaintSpacesInVisibility(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex == -1) return;
            if (_table.Columns[e.ColumnIndex].DividerWidth != _visibilitySpaceWidth) return;

            // We doing force paint of _visible_ part of cell.
            // Since we have frozen columns visible part of cell
            // is not necessarily whole cell.
            var r = e.CellBounds.Left > _table.ReservedColumnsOffset
                ? e.CellBounds
                : new Rectangle(_table.ReservedColumnsOffset + 1, e.CellBounds.Top, e.CellBounds.Right - _table.ReservedColumnsOffset - 1, e.CellBounds.Height);
            r.Width -= _visibilitySpaceWidth;
            e.Graphics.SetClip(r);
            e.Paint(r, DataGridViewPaintParts.All);
            e.Graphics.SetClip(e.CellBounds);
            r = new Rectangle(r.Right - 1, r.Top, _visibilitySpaceWidth + 1, r.Height);
            e.Graphics.FillRectangle(_visibilitySpaceColor, r);
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

            var selectedWatch = _table.GetRowWatchState(_table.Rows[e.RowIndex]);
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

            var typeTextPos = new PointF((float)e.RowBounds.Left + 7, (float)e.RowBounds.Top + 4);
            e.Graphics.DrawString(selectedWatch.Type.ShortName(), _table.Font, Brushes.Black, typeTextPos);
        }
    }
}
