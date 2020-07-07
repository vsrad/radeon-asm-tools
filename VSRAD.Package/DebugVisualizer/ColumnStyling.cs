using System;
using System.Collections.Generic;
using System.Windows.Forms;

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
            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                if (i >= _computedStyling.ColumnState.Length)
                {
                    column.Visible = false;
                    continue;
                }
                column.Visible = (_computedStyling.ColumnState[i] & ColumnStates.Visible) != 0;
                if (column.Visible)
                {
                    if ((_computedStyling.ColumnState[i] & ColumnStates.HasHiddenColumnSeparator) != 0)
                        column.DividerWidth = _hiddenColumnSeparatorWidth;
                    else if ((_computedStyling.ColumnState[i] & ColumnStates.HasLaneSeparator) != 0)
                        column.DividerWidth = _laneDividerWidth;
                    else
                        column.DividerWidth = 0;

                    var bgColor = DataHighlightColors.GetFromColorString(_columnBackgroundColors, i);
                    var fgColor = DataHighlightColors.GetFromColorString(_columnForegroundColors, i);
                    column.DefaultCellStyle.BackColor = _fontAndColor.HighlightBackground[(int)bgColor];
                    column.DefaultCellStyle.ForeColor = _fontAndColor.HighlightForeground[(int)fgColor];
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
