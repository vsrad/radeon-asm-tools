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
            if (_table.SelectedWatch == null ||
                e.RowIndex < 0 ||
                e.ColumnIndex < SliceVisualizerTable.DataColumnOffset ||
                e.ColumnIndex >= SliceVisualizerTable.DataColumnOffset + _table.SelectedWatch.ColumnCount)
                return;

            var relValue = _table.SelectedWatch.GetRelativeValue(e.RowIndex, e.ColumnIndex - SliceVisualizerTable.DataColumnOffset);

            Color maxColor, minColor;
            if (relValue < 0.5f)
            {
                relValue *= 2;
                maxColor = _fontAndColor.FontAndColorState.HeatmapBackground[(int)HeatmapColor.Mean];
                minColor = _fontAndColor.FontAndColorState.HeatmapBackground[(int)HeatmapColor.Cold];
            }
            else
            {
                relValue -= 0.5f;
                maxColor = _fontAndColor.FontAndColorState.HeatmapBackground[(int)HeatmapColor.Hot];
                minColor = _fontAndColor.FontAndColorState.HeatmapBackground[(int)HeatmapColor.Mean];
            }

            var rDiff = maxColor.R - minColor.R;
            var gDiff = maxColor.G - minColor.G;
            var bDiff = maxColor.B - minColor.B;

            var color = Color.FromArgb(
                (byte)(minColor.R + (rDiff * relValue)),
                (byte)(minColor.G + (gDiff * relValue)),
                (byte)(minColor.B + (bDiff * relValue))
            );

            e.CellStyle.BackColor = color;
        }
    }
}
