using System;
using System.Drawing;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class DataFontAndColor
    {
        public Color[] HighlightForeground { get; }
        public Color[] HighlightBackground { get; }
        public bool[] HighlightBold { get; }

        public DataFontAndColor(IFontAndColorProvider provider)
        {
            var colors = (DataHighlightColor[])Enum.GetValues(typeof(DataHighlightColor));

            HighlightForeground = new Color[colors.Length];
            HighlightBackground = new Color[colors.Length];
            HighlightBold = new bool[colors.Length];

            foreach (var highlight in colors)
            {
                var (fg, bg, bold) = provider.GetHighlightInfo(highlight);
                HighlightForeground[(int)highlight] = fg;
                HighlightBackground[(int)highlight] = bg;
                HighlightBold[(int)highlight] = bold;
            }
        }
    }
}
