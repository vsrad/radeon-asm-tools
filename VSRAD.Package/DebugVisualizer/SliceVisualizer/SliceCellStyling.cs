using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    sealed class SliceCellStyling
    {
        private readonly SliceVisualizerTable _table;
        private readonly IFontAndColorProvider _fontAndColor;

        public SliceCellStyling(SliceVisualizerTable table, IFontAndColorProvider fontAndColor)
        {
            _table = table;
            _fontAndColor = fontAndColor;

            _table.CellPainting += HandleCellPaint;
        }

        private void HandleCellPaint(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (_table.SelectedWatch == null || e.RowIndex < 0 || e.ColumnIndex < SliceVisualizerTable.DataColumnOffset)
                return;

            var maxColor = Color.Red;
            var minColor = Color.Green;

            var rDiff = maxColor.R - minColor.R;
            var gDiff = maxColor.G - minColor.G;
            var bDiff = maxColor.B - minColor.B;

            var relValue = _table.SelectedWatch.GetRelativeValue(e.RowIndex, e.ColumnIndex - SliceVisualizerTable.DataColumnOffset);

            var color = Color.FromArgb(
                (byte)(minColor.R + (rDiff * relValue)),
                (byte)(minColor.G + (gDiff * relValue)),
                (byte)(minColor.B + (bDiff * relValue))
            );

            e.CellStyle.BackColor = color;
        }
    }
}
