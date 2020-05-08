using System.Collections.Generic;
using System.Text;

namespace VSRAD.Package.DebugVisualizer
{
    public enum DataHighlightColor
    {
        None = 0,
        ColumnRed, ColumnGreen, ColumnBlue,
        RowRed, RowGreen, RowBlue,
        Inactive
    }

    public static class DataHighlightColors
    {
        public static DataHighlightColor GetFromColorString(string colorString, int index)
        {
            if (colorString == null || index >= colorString.Length)
                return DataHighlightColor.None;

            switch (colorString[index])
            {
                case 'r': return DataHighlightColor.ColumnRed;
                case 'g': return DataHighlightColor.ColumnGreen;
                case 'b': return DataHighlightColor.ColumnBlue;
                default: return DataHighlightColor.None;
            }
        }

        private static char GetCharRepresentation(this DataHighlightColor color)
        {
            switch (color)
            {
                case DataHighlightColor.ColumnRed: return 'r';
                case DataHighlightColor.ColumnGreen: return 'g';
                case DataHighlightColor.ColumnBlue: return 'b';
                default: return ' ';
            }
        }

        public static string UpdateColorStringRange(string colorString, IEnumerable<int> indexes, DataHighlightColor newColor)
        {
            StringBuilder colors = new StringBuilder(VisualizerTable.DataColumnCount);
            if (colorString?.Length == VisualizerTable.DataColumnCount)
                colors.Append(colorString);
            else
                colors.Append(' ', VisualizerTable.DataColumnCount);

            foreach (var i in indexes)
                if (i >= 0 && i < VisualizerTable.DataColumnCount)
                    colors[i] = newColor.GetCharRepresentation();

            colorString = colors.ToString();
            if (string.IsNullOrWhiteSpace(colorString))
                return "";
            return colorString;
        }
    }
}
