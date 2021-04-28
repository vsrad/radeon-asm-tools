using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    sealed class SliceRowStyling
    {
        private readonly SliceVisualizerTable _table;

        public SliceRowStyling(SliceVisualizerTable table)
        {
            _table = table;
        }

        public static void ApplyOnRowPostPaint(SliceVisualizerTable table)
        {
            var rowSyling = new SliceRowStyling(table);
            table.RowPostPaint += rowSyling.ReplaceDefaultRowHeaderBitmap;
        }

        private void ReplaceDefaultRowHeaderBitmap(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (_table.Rows[e.RowIndex].HeaderCell.Value == null) return;
            e.PaintHeader(
                   DataGridViewPaintParts.Background
                   | DataGridViewPaintParts.Border
                   | DataGridViewPaintParts.Focus
                   | DataGridViewPaintParts.SelectionBackground
                   | DataGridViewPaintParts.ContentForeground
               );
            var typeTextPos = new PointF((float)e.RowBounds.Left + 7, (float)e.RowBounds.Top + 4);
            e.Graphics.DrawString(_table.Rows[e.RowIndex].HeaderCell.Value.ToString(),
                _table.RowHeadersDefaultCellStyle.Font,
                new SolidBrush(_table.RowHeadersDefaultCellStyle.ForeColor),
                typeTextPos);
        }
    }
}
