using System;
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
        private readonly SliceColumnStyling _columnStyling;
        private readonly Options.VisualizerAppearance _appearance;
        private readonly ColumnStylingOptions _stylingOptions;

        public SliceCellStyling(SliceVisualizerTable table, TableState state, SliceColumnStyling styling, IFontAndColorProvider fontAndColor, Options.VisualizerAppearance appearance, ColumnStylingOptions stylingOptions)
        {
            _table = table;
            _state = state;
            _fontAndColor = fontAndColor;
            _tableBackgroundBrush = new SolidBrush(table.BackgroundColor);
            _columnStyling = styling;
            _appearance = appearance;
            _stylingOptions = stylingOptions;

            _table.CellPainting += HandleCellPaint;
        }

        private void HandleCellPaint(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (_table.SelectedWatch == null ||
                e.ColumnIndex < SliceVisualizerTable.DataColumnOffset ||
                e.ColumnIndex >= SliceVisualizerTable.DataColumnOffset + _table.SelectedWatch.ColumnCount)
                return;

            if (_table.SelectedWatch.IsInactiveCell(e.RowIndex, e.ColumnIndex - SliceVisualizerTable.DataColumnOffset))
            {
                e.CellStyle.ForeColor = _fontAndColor.FontAndColorState.HighlightForeground[(int)DataHighlightColor.None];
                e.CellStyle.BackColor = _fontAndColor.FontAndColorState.HighlightBackground[(int)DataHighlightColor.Inactive];
                return;
            }

            if (_table.HeatMapMode && e.RowIndex >= 0)
            {
                HandleHeatMap(e);
            }
            else
            {
                /*
                 * TODO: implement any data column count
                var colorIndex = e.ColumnIndex % VisualizerTable.DataColumnCount == 0
                    ? VisualizerTable.DataColumnCount - SliceVisualizerTable.DataColumnOffset
                    : e.ColumnIndex % VisualizerTable.DataColumnCount - SliceVisualizerTable.DataColumnOffset;
                var bgColor = DataHighlightColors.GetFromColorString(_stylingOptions.BackgroundColors, colorIndex);
                e.CellStyle.BackColor = _fontAndColor.FontAndColorState.HighlightBackground[(int)bgColor];
                e.CellStyle.ForeColor = _fontAndColor.FontAndColorState.HighlightForeground[(int)DataHighlightColor.None];
                */
            }
            PaintColumnSeparators(e.ColumnIndex - SliceVisualizerTable.DataColumnOffset, e);
        }
        private void HidePhantomColumn(DataGridViewCellPaintingEventArgs e)
        {
            e.Graphics.FillRectangle(_tableBackgroundBrush, e.CellBounds);
            e.Handled = true;
        }

        private void PaintColumnSeparators(int dataColumnIndex, DataGridViewCellPaintingEventArgs e)
        {
            int width;
            SolidBrush color;

            if ((_columnStyling[dataColumnIndex] & ColumnStates.HasHiddenColumnSeparator) != 0)
            {
                width = _appearance.SliceHiddenColumnSeparatorWidth;
                color = _fontAndColor.FontAndColorState.SliceHiddenColumnSeparatorBrush;
            }
            else if ((_columnStyling[dataColumnIndex] & ColumnStates.HasSubgroupSeparator) != 0)
            {
                width = _appearance.SliceSubgroupSeparatorWidth;
                color = _fontAndColor.FontAndColorState.SliceSubgroupSeparatorBrush;
            }
            else return;

            // We doing force paint of _visible_ part of cell.
            // Since we have frozen columns visible part of cell
            // is not necessarily whole cell.
            var r = e.CellBounds.Left > _table.RowHeadersWidth
                ? e.CellBounds
                : new Rectangle(_table.RowHeadersWidth + 1, e.CellBounds.Top, e.CellBounds.Right - _table.RowHeadersWidth - 1, e.CellBounds.Height);
            r.Width -= width;
            e.Graphics.SetClip(r);
            e.Paint(r, DataGridViewPaintParts.All);
            e.Graphics.SetClip(e.CellBounds);
            r = new Rectangle(r.Right - 1, r.Top, width + 1, r.Height);
            e.Graphics.FillRectangle(color, r);
            e.Graphics.ResetClip();
            e.Handled = true;
        }

        private void HandleHeatMap(DataGridViewCellPaintingEventArgs e)
        {
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
