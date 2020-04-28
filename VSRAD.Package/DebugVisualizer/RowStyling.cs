using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class RowStyling
    {
        private static void GreyOutRow(DataGridViewRow row) => row.DefaultCellStyle.BackColor = Color.LightGray;

        private static void RestoreRowDefaultColor(DataGridViewRow row) => row.DefaultCellStyle.BackColor = Color.Empty;

        public static void ChangeRowFontColor(IEnumerable<DataGridViewRow> rows, Color color)
        {
            foreach (var row in rows)
                row.DefaultCellStyle.ForeColor = color;
        }

        public static void ResetRowStyling(IEnumerable<DataGridViewRow> rows)
        {
            foreach (var row in rows)
                RestoreRowDefaultColor(row);
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
