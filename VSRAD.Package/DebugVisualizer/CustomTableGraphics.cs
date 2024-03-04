using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class CustomTableGraphics
    {
        private readonly VisualizerTable _table;

        public CustomTableGraphics(VisualizerTable table)
        {
            _table = table;
            _table.RowPostPaint += ReplaceDefaultRowHeaderBitmap;
        }

        // Show variable type in row header
        private void ReplaceDefaultRowHeaderBitmap(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (e.RowIndex == _table.NewWatchRowIndex) return;

            var selectedWatch = VisualizerTable.GetRowWatchState(_table.Rows[e.RowIndex]);

            e.PaintHeader(
                    DataGridViewPaintParts.Background
                    | DataGridViewPaintParts.Border
                    | DataGridViewPaintParts.Focus
                    | DataGridViewPaintParts.SelectionBackground
                    //| DataGridViewPaintParts.ContentForeground // not needed because we are using custom string painter below
                );

            if (Watch.IsWatchNameValid(selectedWatch.Name))
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
