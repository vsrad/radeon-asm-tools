using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class CustomTableGraphics
    {
        private readonly VisualizerTable _table;
        private static readonly Brush _avgprColor = Brushes.LightGreen;

        public CustomTableGraphics(VisualizerTable table)
        {
            _table = table;
            _table.RowPostPaint += ReplaceDefaultRowHeaderBitmap;
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
                e.Graphics.DrawString(selectedWatch.Info.ShortName(),
                    _table.RowHeadersDefaultCellStyle.Font,
                    new SolidBrush(_table.RowHeadersDefaultCellStyle.ForeColor),
                    typeTextPos);
            }
        }
    }
}
