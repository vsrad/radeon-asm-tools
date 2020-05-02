using Microsoft.VisualStudio.Shell;
using System;
using System.Drawing;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class FontAndColorState
    {
        public Color[] HighlightForeground { get; }
        public Color[] HighlightBackground { get; }
        public bool[] HighlightBold { get; }

        public Color HeaderForeground { get; }
        public Color HeaderBackground { get; }
        public Color WatchNameBackground { get; }
        public Color WatchNameForeground { get; }
        public bool HeaderBold { get; }
        public bool WatchNameBold { get; }

        public SolidBrush ColumnSeparatorBrush { get; }
        public SolidBrush HiddenColumnSeparatorBrush { get; }

        public Font RegularFont { get; }
        public Font BoldFont { get; }

        public FontAndColorState(FontAndColorProvider provider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var highlightColors = (DataHighlightColor[])Enum.GetValues(typeof(DataHighlightColor));

            HighlightForeground = new Color[highlightColors.Length];
            HighlightBackground = new Color[highlightColors.Length];
            HighlightBold = new bool[highlightColors.Length];

            foreach (var color in highlightColors)
            {
                var (fg, bg, bold) = provider.GetHighlightInfo(color);
                HighlightForeground[(int)color] = fg;
                HighlightBackground[(int)color] = bg;
                HighlightBold[(int)color] = bold;
            }

            (HeaderForeground, HeaderBackground, HeaderBold) = provider.GetInfo(FontAndColorItem.Header);
            (WatchNameForeground, WatchNameBackground, WatchNameBold) = provider.GetInfo(FontAndColorItem.WatchNames);

            ColumnSeparatorBrush = new SolidBrush(provider.GetInfo(FontAndColorItem.ColumnSeparator).bg);
            HiddenColumnSeparatorBrush = new SolidBrush(provider.GetInfo(FontAndColorItem.HiddenColumnSeparator).bg);

            var (fontName, fontSize) = provider.GetFontInfo();
            RegularFont = new Font(fontName, fontSize, FontStyle.Regular);
            BoldFont = new Font(fontName, fontSize, FontStyle.Bold);
        }

        // For testing
        public FontAndColorState() { }
    }
}
