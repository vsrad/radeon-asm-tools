using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnStyling
    {
        private readonly uint _laneGrouping;
        private readonly uint _laneDividerWidth;
        private readonly uint _hiddenColumnSeparatorWidth;
        private readonly string _columnBackgroundColors;
        private readonly string _columnForegroundColors;

        private readonly FontAndColorState _fontAndColor;

        private readonly bool[] _visibility = new bool[VisualizerTable.DataColumnCount];

        public ColumnStyling(Options.VisualizerOptions options, Options.VisualizerAppearance appearance, ColumnStylingOptions styling, FontAndColorState fontAndColor)
        {
            _laneGrouping = options.VerticalSplit ? options.LaneGrouping : 0;
            _laneDividerWidth = (uint)appearance.LaneDivierWidth;
            _hiddenColumnSeparatorWidth = (uint)appearance.HiddenColumnSeparatorWidth;
            _columnBackgroundColors = styling.BackgroundColors;
            _columnForegroundColors = styling.ForegroundColors;

            _fontAndColor = fontAndColor;

            foreach (int index in ColumnSelector.ToIndexes(styling.VisibleColumns))
                _visibility[index] = true;
        }

        public void Apply(IReadOnlyList<DataGridViewColumn> columns, uint groupSize)
        {
            if (columns.Count != VisualizerTable.DataColumnCount)
                throw new ArgumentException("ColumnAppearance applies to exactly 512 columns");

            for (var i = 0; i < VisualizerTable.DataColumnCount; i++)
                columns[i].Visible = false;

            /* IMPORTANT: Columns must be made invisible before changing DividerWidth, otherwise the loop takes _seconds_ to execute.
             * The reference source (https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/DataGridViewMethods.cs,396b1c43b2c82004,references)
             * shows that changing the width invokes OnColumnGlobalAutoSize (unless the column is invisible).
             * I'm not sure if it really is the cause of the slowdown but setting Visible to false removes it. */
            for (int i = 0; i < groupSize; i++)
                columns[i].DividerWidth = 0;

            ApplyLaneGrouping(columns, groupSize);

            for (int i = 0; i < groupSize; i++)
            {
                columns[i].DefaultCellStyle.BackColor = _fontAndColor.HighlightBackground[(int)DataHighlightColors.GetFromColorString(_columnBackgroundColors, i)];
                columns[i].DefaultCellStyle.ForeColor = _fontAndColor.HighlightForeground[(int)DataHighlightColors.GetFromColorString(_columnForegroundColors, i)];
                columns[i].Visible = _visibility[i];
                if (i != groupSize - 1 && _visibility[i] != _visibility[i + 1])
                    columns[i].DividerWidth = (int)_hiddenColumnSeparatorWidth;
            }
        }

        private void ApplyLaneGrouping(IReadOnlyList<DataGridViewColumn> columns, uint groupSize)
        {
            if (_laneGrouping == 0)
                return;
            for (uint start = 0; start < groupSize - _laneGrouping; start += _laneGrouping)
            {
                for (int lastVisibleInGroup = (int)Math.Min(start + _laneGrouping - 1, groupSize - 1);
                    lastVisibleInGroup >= start; lastVisibleInGroup--)
                {
                    if (_visibility[lastVisibleInGroup])
                    {
                        columns[lastVisibleInGroup].DividerWidth = (int)_laneDividerWidth;
                        break;
                    }
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
