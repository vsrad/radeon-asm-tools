using System.Drawing;

namespace VSRAD.Package.DebugVisualizer
{
    public enum ColumnHighlightColor
    {
        Red, Green, Blue
    }

    public static class ColumnHighlightColorMapping
    {
        public static Color AsColor(this ColumnHighlightColor color)
        {
            switch (color)
            {
                case ColumnHighlightColor.Red:
                    return Colors.RedHighlight;
                case ColumnHighlightColor.Green:
                    return Colors.GreenHighlight;
                case ColumnHighlightColor.Blue:
                    return Colors.BlueHighlight;
                default:
                    return Color.Empty;
            }
        }
    }
}
