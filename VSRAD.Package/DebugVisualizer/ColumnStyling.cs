using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnStyling
    {
        private readonly uint _laneGrouping;
        private readonly uint _laneDividerWidth;
        private readonly uint _hiddenColumnSeparatorWidth;

        private readonly DataFontAndColor _fontAndColor;

        private readonly bool[] _visibility = new bool[VisualizerTable.DataColumnCount];
        private readonly DataHighlightColor[] _highlight = new DataHighlightColor[VisualizerTable.DataColumnCount];

        public ColumnStyling(Options.VisualizerOptions options, Options.VisualizerAppearance appearance, ColumnStylingOptions styling, DataFontAndColor fontAndColor)
        {
            _laneGrouping = options.VerticalSplit ? options.LaneGrouping : 0;
            _laneDividerWidth = (uint)appearance.LaneDivierWidth;
            _hiddenColumnSeparatorWidth = (uint)appearance.HiddenColumnSeparatorWidth;
            _fontAndColor = fontAndColor;

            foreach (int index in ColumnSelector.ToIndexes(styling.VisibleColumns))
                _visibility[index] = true;

            foreach (var region in styling.HighlightRegions)
                foreach (var index in ColumnSelector.ToIndexes(region.Selector))
                    _highlight[index] = region.Color;
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

            if (_laneGrouping != 0)
            {
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

            for (int i = 0; i < groupSize; i++)
            {
                columns[i].DefaultCellStyle.BackColor = _fontAndColor.HighlightBackground[(int)_highlight[i]];
                columns[i].DefaultCellStyle.ForeColor = _fontAndColor.HighlightForeground[(int)_highlight[i]];
                columns[i].Visible = _visibility[i];
                if (i != groupSize - 1 && _visibility[i] != _visibility[i + 1])
                    columns[i].DividerWidth = (int)_hiddenColumnSeparatorWidth;
            }
        }
        public static void ApplyLaneMask(IReadOnlyList<DataGridViewColumn> columns, uint groupSize, uint[] system)
        {
            if (columns.Count != VisualizerTable.DataColumnCount)
                throw new ArgumentException($"Lane mask applies to exactly {VisualizerTable.DataColumnCount} columns");

            for (int wfrontOffset = 0; wfrontOffset < groupSize; wfrontOffset += 64)
            {
                var execMask = new BitArray(new int[] { (int)system[wfrontOffset + 8], (int)system[wfrontOffset + 9] });

                for (int laneId = 0; laneId < 64; laneId++)
                    if (!execMask[laneId])
                        columns[wfrontOffset + laneId].DefaultCellStyle.BackColor = Color.LightGray;
            }
        }

        public static void ApplyMagicNumber(IReadOnlyList<DataGridViewColumn> columns, uint groupSize, uint[] system, int magicNumber)
        {
            for (int i = 0; i < groupSize; i += 64)
                if (system[i] != magicNumber)
                    GrayOutColumns(columns, groupSize, i, (uint)(i + 64));
        }

        public static void GrayOutColumns(IReadOnlyList<DataGridViewColumn> columns, uint groupSize, int start = 0, uint end = 0)
        {
            for (int offset = start; offset < ((end == 0) ? groupSize : end); offset++)
                columns[offset].DefaultCellStyle.BackColor = Color.LightGray;
        }
    }
}
