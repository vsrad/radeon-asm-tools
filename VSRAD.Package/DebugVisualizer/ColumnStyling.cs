using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnStyling
    {
        private readonly int _laneDividerWidth;
        private readonly int _hiddenColumnSeparatorWidth;
        private readonly string _columnBackgroundColors;
        private readonly string _columnForegroundColors;

        private readonly FontAndColorState _fontAndColor;
        private readonly ComputedColumnStyling _computedStyling;

        public ColumnStyling(Options.VisualizerAppearance appearance, ColumnStylingOptions styling, ComputedColumnStyling computedStyling, FontAndColorState fontAndColor)
        {
            _laneDividerWidth = appearance.LaneSeparatorWidth;
            _hiddenColumnSeparatorWidth = appearance.HiddenColumnSeparatorWidth;
            _columnBackgroundColors = styling.BackgroundColors;
            _columnForegroundColors = styling.ForegroundColors;

            _computedStyling = computedStyling;
            _fontAndColor = fontAndColor;
        }

        public void Apply(IReadOnlyList<DataGridViewColumn> columns)
        {
            if (columns.Count != VisualizerTable.DataColumnCount)
                throw new ArgumentException("ColumnAppearance applies to exactly 512 columns");

            for (int i = 0; i < _computedStyling.GroupSize; i++)
            {
                columns[i].Visible = (_computedStyling.ColumnState[i] & ColumnStates.Visible) != 0;
                if (columns[i].Visible)
                {
                    if ((_computedStyling.ColumnState[i] & ColumnStates.HasHiddenColumnSeparator) != 0)
                        columns[i].DividerWidth = _hiddenColumnSeparatorWidth;
                    else if ((_computedStyling.ColumnState[i] & ColumnStates.HasLaneSeparator) != 0)
                        columns[i].DividerWidth = _laneDividerWidth;
                    else
                        columns[i].DividerWidth = 0;

                    var bgColor = DataHighlightColors.GetFromColorString(_columnBackgroundColors, i);
                    var fgColor = DataHighlightColors.GetFromColorString(_columnForegroundColors, i);
                    columns[i].DefaultCellStyle.BackColor = _fontAndColor.HighlightBackground[(int)bgColor];
                    columns[i].DefaultCellStyle.ForeColor = _fontAndColor.HighlightForeground[(int)fgColor];
                }
            }
        }

        public static void ApplyHeatMap(IReadOnlyList<DataGridViewRow> rows, Color minColor, Color maxColor)
        {
            var minValue = rows
                .Select(r => r.Cells)
                .Select(c => c
                    .Cast<DataGridViewCell>()
                    .Where(cell => cell.Value != null)
                    .Min(cell => int.Parse(cell.Value?.ToString())))
                .Min();
            var maxValue = rows
                .Select(r => r.Cells)
                .Select(c => c
                    .Cast<DataGridViewCell>()
                    .Where(cell => cell.Value != null)
                    .Max(cell => int.Parse(cell.Value?.ToString())))
                .Max();

            var valueDiff = maxValue - minValue;
            var rDiff = maxColor.R - minColor.R;
            var gDiff = maxColor.G - minColor.G;
            var bDiff = maxColor.B - minColor.B;

            foreach (var row in rows)
            {
                foreach (var cell in row.Cells.Cast<DataGridViewCell>())
                {
                    if (cell.Value == null) continue;
                    var value = int.Parse(cell.Value.ToString());
                    var relDiff = ((float)(value - minValue)) / valueDiff;
                    var color = Color.FromArgb(
                        (byte)(minColor.R + (rDiff * relDiff)),
                        (byte)(minColor.G + (gDiff * relDiff)),
                        (byte)(minColor.B + (bDiff * relDiff))
                    );
                    cell.Style.BackColor = color;
                }
            }
        }

        public static void GrayOutColumns(IReadOnlyList<DataGridViewColumn> columns, FontAndColorState fontAndColor, uint groupSize)
        {
            for (int i = 0; i < groupSize; i++)
                columns[i].DefaultCellStyle.BackColor = fontAndColor.HighlightBackground[(int)DataHighlightColor.Inactive];
        }
    }
}
