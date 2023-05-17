using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class RowStyling
    {
        public static void UpdateRowHighlight(DataGridViewRow row, FontAndColorState colors, DataHighlightColor? changeFg = null, DataHighlightColor? changeBg = null)
        {
            var rowFg = DataHighlightColor.None;
            var rowBg = DataHighlightColor.None;

            if (row.DefaultCellStyle.Tag is ValueTuple<DataHighlightColor, DataHighlightColor> existingColors)
                (rowFg, rowBg) = existingColors;

            if (changeFg is DataHighlightColor fg)
                rowFg = fg;
            if (changeBg is DataHighlightColor bg)
                rowBg = bg;

            var fgColor = rowFg != DataHighlightColor.None ? colors.HighlightForeground[(int)rowFg] : Color.Empty;
            var bgColor = rowBg != DataHighlightColor.None ? colors.HighlightBackground[(int)rowBg] : Color.Empty;

            row.DefaultCellStyle.ForeColor = fgColor;
            row.DefaultCellStyle.BackColor = bgColor;
            row.DefaultCellStyle.Tag = (rowFg, rowBg);
        }

        public static void GrayOutRow(FontAndColorState colors, DataGridViewRow row)
        {
            row.DefaultCellStyle.BackColor = colors.HighlightBackground[(int)DataHighlightColor.Inactive];
        }
    }
}
