using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnStyling
    {
        public bool[] Visibility = new bool[VisualizerTable.DataColumnCount];
        private readonly Color[] _highlightColor = new Color[VisualizerTable.DataColumnCount];

        public ColumnStyling(string visibleColumns, IEnumerable<ColumnHighlightRegion> highlightRegions)
        {
            Ensure.ArgumentNotNull(visibleColumns, nameof(visibleColumns));
            Ensure.ArgumentNotNull(highlightRegions, nameof(highlightRegions));

            foreach (int index in ColumnSelector.ToIndexes(visibleColumns))
                Visibility[index] = true;

            foreach (var region in highlightRegions)
            {
                foreach (var index in ColumnSelector.ToIndexes(region.Selector))
                    _highlightColor[index] = region.Color.AsColor();
            }
        }

        public void Apply(IReadOnlyList<DataGridViewColumn> columns, uint groupSize, uint laneGrouping, int laneDividerWidth, int hiddenColumnSeparatorWidth)
        {
            if (columns.Count != VisualizerTable.DataColumnCount)
                throw new ArgumentException("ColumnStyling applies to exactly 512 columns");

            for (var i = 0; i < VisualizerTable.DataColumnCount; i++)
                columns[i].Visible = false;

            /* IMPORTANT: Columns must be made invisible before changing DividerWidth, otherwise the loop takes _seconds_ to execute.
             * The reference source (https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/DataGridViewMethods.cs,396b1c43b2c82004,references)
             * shows that changing the width invokes OnColumnGlobalAutoSize (unless the column is invisible).
             * I'm not sure if it really is the cause of the slowdown but setting Visible to false removes it. */
            for (int i = 0; i < groupSize; i++)
                columns[i].DividerWidth = 0;

            if (laneGrouping != 0)
            {
                for (int start = 0; start < groupSize - (int)laneGrouping; start += (int)laneGrouping)
                {
                    for (int lastVisibleInGroup = Math.Min(start + (int)laneGrouping - 1, (int)groupSize - 1);
                        lastVisibleInGroup >= start; lastVisibleInGroup--)
                    {
                        if (Visibility[lastVisibleInGroup])
                        {
                            columns[lastVisibleInGroup].DividerWidth = laneDividerWidth;
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < groupSize; i++)
            {
                columns[i].DefaultCellStyle.BackColor = _highlightColor[i];
                columns[i].Visible = Visibility[i];
                if (i != groupSize - 1 && Visibility[i] != Visibility[i + 1])
                    columns[i].DividerWidth = hiddenColumnSeparatorWidth;
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
