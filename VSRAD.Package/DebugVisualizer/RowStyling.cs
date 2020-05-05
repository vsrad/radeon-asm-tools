using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class RowStyling
    {
        private readonly IEnumerable<DataGridViewRow> _rows;

        private readonly bool _maskInactiveLanes;
        private readonly int? _checkMagicNumber;
        private readonly DataGridViewCellStyle _inactiveCellStyle;

        public RowStyling(IEnumerable<DataGridViewRow> rows, Options.VisualizerOptions options, FontAndColorState fontAndColor)
        {
            _rows = rows;
            _maskInactiveLanes = options.MaskLanes;
            _checkMagicNumber = options.CheckMagicNumber ? (int?)options.MagicNumber : null;
            _inactiveCellStyle = new DataGridViewCellStyle
            {
                // Override row highlight foreground
                ForeColor = fontAndColor.HighlightForeground[(int)DataHighlightColor.None],
                BackColor = fontAndColor.HighlightBackground[(int)DataHighlightColor.Inactive]
            };
        }

        // Column styles that have precedence over row styles need to be set as individual cell styles,
        // because the order in which DataGridView applies styles is column -> row -> cell,
        // with cell styles having the highest priority.
        public void Apply(uint groupSize, uint[] system)
        {
            // Reset previously masked cells
            foreach (var row in _rows)
                foreach (DataGridViewCell cell in row.Cells)
                    cell.Style = null;

            if (system == null)
                return;

            for (int wfrontOffset = 0; wfrontOffset < groupSize; wfrontOffset += 64)
            {
                if (_checkMagicNumber is int magicNumber && system[wfrontOffset] != magicNumber)
                {
                    for (int laneId = 0; laneId < 64; ++laneId)
                        GrayOutColumn(wfrontOffset + laneId);
                }
                else if (_maskInactiveLanes)
                {
                    var execMask = new BitArray(new int[] { (int)system[wfrontOffset + 8], (int)system[wfrontOffset + 9] });
                    for (int laneId = 0; laneId < 64; ++laneId)
                        if (!execMask[laneId])
                            GrayOutColumn(wfrontOffset + laneId);
                }
            }
        }

        private void GrayOutColumn(int columnIndex)
        {
            foreach (var row in _rows)
                row.Cells[VisualizerTable.DataColumnOffset + columnIndex].Style = _inactiveCellStyle;
        }

        public static void ChangeRowHighlight(IEnumerable<DataGridViewRow> rows, FontAndColorState colors, DataHighlightColor color)
        {
            var fg = color != DataHighlightColor.None ? colors.HighlightForeground[(int)color] : Color.Empty;
            var bg = color != DataHighlightColor.None ? colors.HighlightBackground[(int)color] : Color.Empty;
            foreach (var row in rows)
            {
                row.DefaultCellStyle.ForeColor = fg;
                row.DefaultCellStyle.BackColor = bg;
            }
        }

        public static void GrayOutUnevaluatedWatches(IEnumerable<DataGridViewRow> rows, FontAndColorState colors, ReadOnlyCollection<string> watches)
        {
            var inactiveBg = colors.HighlightBackground[(int)DataHighlightColor.Inactive];
            foreach (var row in rows)
            {
                var watch = (string)row.Cells[VisualizerTable.NameColumnIndex].Value;
                var isUnevaluated = !string.IsNullOrWhiteSpace(watch) && watch != "System" && !watches.Contains(watch);

                if (isUnevaluated)
                {
                    // Preserve old background in case the row was highlighted
                    row.DefaultCellStyle.Tag = row.DefaultCellStyle.BackColor;
                    row.DefaultCellStyle.BackColor = inactiveBg;
                }
                else if (row.DefaultCellStyle.BackColor == inactiveBg) // used to be unevaluated
                {
                    if (row.DefaultCellStyle.Tag is Color highlightColor)
                        row.DefaultCellStyle.BackColor = highlightColor;
                    else
                        row.DefaultCellStyle.BackColor = Color.Empty;
                }
            }
        }
    }
}
