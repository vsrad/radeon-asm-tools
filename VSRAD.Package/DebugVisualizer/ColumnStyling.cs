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
            if (columns.Count != VisualizerTable.DataColumnCount)
                throw new ArgumentException("ColumnAppearance applies to exactly 512 columns");

            for (var i = 0; i < VisualizerTable.DataColumnCount; i++)
                columns[i].Visible = false;

            /* IMPORTANT: Columns must be made invisible before changing DividerWidth, otherwise the loop takes _seconds_ to execute.
             * The reference source (https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/DataGridViewMethods.cs,396b1c43b2c82004,references)
             * shows that changing the width invokes OnColumnGlobalAutoSize (unless the column is invisible).
             * I'm not sure if it really is the cause of the slowdown but setting Visible to false removes it. */
            for (int i = 0; i < _computedStyling.GroupSize; i++)
            {
                if ((_computedStyling.ColumnState[i] & ColumnStates.Visible) == 0)
                    continue;

                if ((_computedStyling.ColumnState[i] & ColumnStates.HasHiddenColumnSeparator) != 0)
                    columns[i].DividerWidth = _hiddenColumnSeparatorWidth;
                else if ((_computedStyling.ColumnState[i] & ColumnStates.HasLaneSeparator) != 0)
                    columns[i].DividerWidth = _laneDividerWidth;
                else
                    columns[i].DividerWidth = 0;

                columns[i].DefaultCellStyle.BackColor = _fontAndColor.HighlightBackground[(int)DataHighlightColors.GetFromColorString(_columnBackgroundColors, i)];
                columns[i].DefaultCellStyle.ForeColor = _fontAndColor.HighlightForeground[(int)DataHighlightColors.GetFromColorString(_columnForegroundColors, i)];

                columns[i].Visible = true;
            }
        }
    }
}
