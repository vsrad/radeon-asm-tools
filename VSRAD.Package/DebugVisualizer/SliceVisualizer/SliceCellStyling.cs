﻿using System;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    sealed class SliceCellStyling
    {
        private readonly SliceVisualizerTable _table;
        private readonly IFontAndColorProvider _fontAndColor;
        private readonly SliceColumnStyling _columnStyling;
        private readonly Options.VisualizerAppearance _appearance;
        private readonly ColumnStylingOptions _stylingOptions;
        private readonly SolidBrush _tableBackgroundBrush;

        public SliceCellStyling(SliceVisualizerTable table, SliceColumnStyling styling, IFontAndColorProvider fontAndColor, SliceVisualizerContext context)
        {
            _table = table;
            _fontAndColor = fontAndColor;
            _columnStyling = styling;
            _appearance = context.Options.VisualizerAppearance;
            _stylingOptions = context.Options.VisualizerColumnStyling;
            _tableBackgroundBrush = new SolidBrush(_table.BackgroundColor);
        }

        public static void ApplyCellStylingOnCellPainting(SliceVisualizerTable table, SliceColumnStyling styling, IFontAndColorProvider fontAndColor, SliceVisualizerContext context)
        {
            var cellStyling = new SliceCellStyling(table, styling, fontAndColor, context);
            table.CellPainting += cellStyling.HandleCellPaint;
        }

        public void HandleCellPaint(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (_table.SelectedWatch == null || !_table.SelectedWatchValid)
            {
                e.Graphics.FillRectangle(_tableBackgroundBrush, e.CellBounds);
                e.Handled = true;
                return;
            }

            if ((e.ColumnIndex < SliceVisualizerTable.DataColumnOffset && e.ColumnIndex != _table.PhantomColumnIndex) ||
                e.ColumnIndex >= SliceVisualizerTable.DataColumnOffset + _table.SelectedWatch.ColumnCount)
                return;

            if (e.ColumnIndex == _table.PhantomColumnIndex)
            {
                e.Graphics.FillRectangle(_tableBackgroundBrush, e.CellBounds);
                e.Handled = true;
                return;
            }

            var handled = false;

            if (e.RowIndex >= 0) handled = PaintBackgroud(e);

            // called last because manipulates with e.Handled
            handled |= PaintColumnSeparators(e.ColumnIndex - SliceVisualizerTable.DataColumnOffset, e);
            e.Handled = handled;
        }

        private bool PaintBackgroud(DataGridViewCellPaintingEventArgs e)
        {
            if (_table.SelectedWatch.IsInactiveCell(e.RowIndex, e.ColumnIndex - SliceVisualizerTable.DataColumnOffset))
            {
                e.CellStyle.ForeColor = _fontAndColor.FontAndColorState.HighlightForeground[(int)DataHighlightColor.None];
                e.CellStyle.BackColor = _fontAndColor.FontAndColorState.HighlightBackground[(int)DataHighlightColor.Inactive];
                return false;
            }

            if (_table.HeatMapMode)
            {
                return HandleHeatMap(e);
            }
            else
            {
                var colIndex = e.ColumnIndex - SliceVisualizerTable.DataColumnOffset;
                var colorIndex = colIndex % _table.GroupSize;
                var bgColor = DataHighlightColors.GetFromColorString(_stylingOptions.BackgroundColors, (int)colorIndex);
                e.CellStyle.BackColor = _fontAndColor.FontAndColorState.HighlightBackground[(int)bgColor];
                e.CellStyle.ForeColor = _fontAndColor.FontAndColorState.HighlightForeground[(int)DataHighlightColor.None];
                return false;
            }
        }

        private bool PaintColumnSeparators(int dataColumnIndex, DataGridViewCellPaintingEventArgs e)
        {
            int width;
            SolidBrush color;

            if ((_columnStyling[dataColumnIndex] & ColumnStates.HasSubgroupSeparator) != 0)
            {
                width = _appearance.SliceSubgroupSeparatorWidth;
                color = _fontAndColor.FontAndColorState.SliceSubgroupSeparatorBrush;
            }
            else if ((_columnStyling[dataColumnIndex] & ColumnStates.HasHiddenColumnSeparator) != 0)
            {
                width = _appearance.SliceHiddenColumnSeparatorWidth;
                color = _fontAndColor.FontAndColorState.SliceHiddenColumnSeparatorBrush;
            }
            else return false;

            var overrideColor = !(_table.SelectedWatch.IsSingleWordValue && _table.HeatMapMode && e.RowIndex != -1);

            // We doing force paint of _visible_ part of cell.
            // Since we have frozen columns visible part of cell
            // is not necessarily whole cell.
            var r = e.CellBounds.Left > _table.ReservedColumnsOffset
                ? e.CellBounds
                : new Rectangle(_table.ReservedColumnsOffset + 1, e.CellBounds.Top, e.CellBounds.Right - _table.ReservedColumnsOffset - 1, e.CellBounds.Height);
            CellStyling.PaintContentWithSeparator(width, color, r, e, overrideColor);
            return true;
        }

        private bool HandleHeatMap(DataGridViewCellPaintingEventArgs e)
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
                return true;
            }
            else
            {
                var relValue = _table.SelectedWatch.GetRelativeValue(e.RowIndex, e.ColumnIndex - SliceVisualizerTable.DataColumnOffset);
                e.CellStyle.BackColor = GetHeatmapColor(relValue);
                return false;
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
