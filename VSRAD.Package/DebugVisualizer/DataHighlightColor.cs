using System.Collections.Generic;
using System.Text;

namespace VSRAD.Package.DebugVisualizer
{
    public enum DataHighlightColor
    {
        None = 0, Inactive, Red, Green, Blue
    }

    public static class DataHighlightColors
    {
        public static DataHighlightColor GetFromColorString(string colorString, int index)
        {
            if (colorString == null || index >= colorString.Length)
                return DataHighlightColor.None;

            switch (colorString[index])
            {
                case 'r': return DataHighlightColor.Red;
                case 'g': return DataHighlightColor.Green;
                case 'b': return DataHighlightColor.Blue;
                default: return DataHighlightColor.None;
            }
        }

        private static char GetCharRepresentation(this DataHighlightColor color)
        {
            switch (color)
            {
                case DataHighlightColor.Red: return 'r';
                case DataHighlightColor.Green: return 'g';
                case DataHighlightColor.Blue: return 'b';
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
