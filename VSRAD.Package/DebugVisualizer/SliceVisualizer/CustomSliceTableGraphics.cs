using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    class CustomSliceTableGraphics
    {
        private readonly SliceVisualizerTable _table;

        public CustomSliceTableGraphics(SliceVisualizerTable table)
        {
            _table = table;
            _table.RowPostPaint += ReplaceDefaultRowHeaderBitmap;
        }


        private void ReplaceDefaultRowHeaderBitmap(object sender, DataGridViewRowPostPaintEventArgs e)
        {
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
