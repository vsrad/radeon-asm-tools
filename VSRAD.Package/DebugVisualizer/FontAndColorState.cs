using Microsoft.VisualStudio.Shell;
using System;
using System.Drawing;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class FontAndColorState
    {
        private static readonly DataHighlightColor[] _highlightColorVariants = (DataHighlightColor[])Enum.GetValues(typeof(DataHighlightColor));
        private static readonly HeatmapColor[] _heatmapColorVariants = (HeatmapColor[])Enum.GetValues(typeof(HeatmapColor));

        public Color[] HighlightForeground { get; } = new Color[_highlightColorVariants.Length];
        public Color[] HighlightBackground { get; } = new Color[_highlightColorVariants.Length];
        public bool[] HighlightBold { get; } = new bool[_highlightColorVariants.Length];

        public Color[] HeatmapBackground { get; } = new Color[_heatmapColorVariants.Length];

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

            foreach (var color in _highlightColorVariants)
            {
                var (fg, bg, bold) = provider.GetHighlightInfo(color);
                HighlightForeground[(int)color] = fg;
                HighlightBackground[(int)color] = bg;
                HighlightBold[(int)color] = bold;
            }
            foreach (var color in _heatmapColorVariants)
            {
                var (_, bg, _) = provider.GetInfo(color);
                HeatmapBackground[(int)color] = bg;
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
