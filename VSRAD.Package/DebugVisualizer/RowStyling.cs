using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class RowStyling
    {
        private static void GreyOutRow(DataGridViewRow row) => row.DefaultCellStyle.BackColor = Color.LightGray;

        public static void ChangeRowHighlight(FontAndColorState colors, IEnumerable<DataGridViewRow> rows, DataHighlightColor color)
        {
            if (color != DataHighlightColor.None)
                foreach (var row in rows)
                {
                    row.DefaultCellStyle.ForeColor = colors.HighlightForeground[(int)color];
                    row.DefaultCellStyle.BackColor = colors.HighlightBackground[(int)color];
                }
            else
                ResetRowStyling(rows);
        }

        public static void ResetRowStyling(IEnumerable<DataGridViewRow> rows)
        {
            foreach (var row in rows)
            {
                row.DefaultCellStyle.ForeColor = Color.Empty;
                row.DefaultCellStyle.BackColor = Color.Empty;
            }
        }

        public static void GreyOutUnevaluatedWatches(ReadOnlyCollection<string> watches, IEnumerable<DataGridViewRow> rows)
        {
            foreach (var row in rows)
            {
                var rowWatch = (string)row.Cells[VisualizerTable.NameColumnIndex].Value;
                if (!string.IsNullOrWhiteSpace(rowWatch) && !watches.Contains(rowWatch))
                    GreyOutRow(row);
            }
        }
    }
}
