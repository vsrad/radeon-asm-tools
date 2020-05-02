using System;
using System.Drawing;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class FontAndColorState
    {
        public Color[] HighlightForeground { get; }
        public Color[] HighlightBackground { get; }
        public bool[] HighlightBold { get; }

        public SolidBrush ColumnSeparatorBrush { get; }
        public SolidBrush HiddenColumnSeparatorBrush { get; }

        public FontAndColorState(IFontAndColorProvider provider)
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

            ColumnSeparatorBrush = new SolidBrush(provider.GetInfo(FontAndColorItem.ColumnSeparator).bg);
            HiddenColumnSeparatorBrush = new SolidBrush(provider.GetInfo(FontAndColorItem.HiddenColumnSeparator).bg);
        }
    }
}
