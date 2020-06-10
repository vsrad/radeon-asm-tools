using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    sealed class SliceCellStyling
    {
        private readonly SliceVisualizerTable _table;
        private readonly TableState _state;
        private readonly IFontAndColorProvider _fontAndColor;
        private readonly SolidBrush _tableBackgroundBrush;

        public SliceCellStyling(SliceVisualizerTable table, TableState state, IFontAndColorProvider fontAndColor)
        {
            _table = table;
            _state = state;
            _fontAndColor = fontAndColor;
            _tableBackgroundBrush = new SolidBrush(table.BackgroundColor);

            _table.CellPainting += HandleCellPaint;
        }

        private void HandleCellPaint(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex == _state.PhantomColumnIndex)
            {
                HidePhantomColumn(e);
                return;
            }

            if (_table.SelectedWatch == null ||
                e.RowIndex < 0 ||
                e.ColumnIndex < SliceVisualizerTable.DataColumnOffset ||
                e.ColumnIndex >= SliceVisualizerTable.DataColumnOffset + _table.SelectedWatch.ColumnCount)
                return;

            if (_table.SelectedWatch.IsSingleWordValue)
            {
                var rel1 = _table.SelectedWatch.GetRelativeValue(e.RowIndex, e.ColumnIndex - SliceVisualizerTable.DataColumnOffset, word: 0);
                var rel2 = _table.SelectedWatch.GetRelativeValue(e.RowIndex, e.ColumnIndex - SliceVisualizerTable.DataColumnOffset, word: 1);
                var brush1 = new SolidBrush(GetHeatmapColor(rel1));
                var brush2 = new SolidBrush(GetHeatmapColor(rel2));

                e.Graphics.FillRectangle(brush1, new Rectangle(e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Width / 2, e.CellBounds.Height));
                e.Graphics.FillRectangle(brush2, new Rectangle(e.CellBounds.Left + e.CellBounds.Width / 2, e.CellBounds.Top, e.CellBounds.Width / 2, e.CellBounds.Height));
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.Background);
                e.Handled = true;
            }
            else
            {
                var relValue = _table.SelectedWatch.GetRelativeValue(e.RowIndex, e.ColumnIndex - SliceVisualizerTable.DataColumnOffset);
                e.CellStyle.BackColor = GetHeatmapColor(relValue);
            }
        }

        private void HidePhantomColumn(DataGridViewCellPaintingEventArgs e)
        {
            e.Graphics.FillRectangle(_tableBackgroundBrush, e.CellBounds);
            e.Handled = true;
        }

        private Color GetHeatmapColor(float relValue)
        {
            if (float.IsNaN(relValue))
                return _fontAndColor.FontAndColorState.HighlightBackground[(int)DataHighlightColor.Inactive];

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

            return Color.FromArgb(
                (byte)(minColor.R + (rDiff * relValue)),
                (byte)(minColor.G + (gDiff * relValue)),
                (byte)(minColor.B + (bDiff * relValue))
            );
        }
    }
}
