using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Drawing;
using static Microsoft.VisualStudio.Shell.Package;

namespace VSRAD.Package.DebugVisualizer
{
    public interface IFontAndColorProvider
    {
        FontAndColorState FontAndColorState { get; }

        (Color fg, Color bg, bool bold) GetInfo(FontAndColorItem item);
        (Color fg, Color bg, bool bold) GetHighlightInfo(DataHighlightColor highlight);
    }

    sealed class FontAndColorProvider : IFontAndColorProvider
    {
        public event Action FontAndColorInfoChanged;

        public FontAndColorState FontAndColorState { get; private set; }

        private readonly IVsFontAndColorStorage _storage;
        private const uint _storageFlags = (uint)(__FCSTORAGEFLAGS.FCSF_LOADDEFAULTS
            | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES
            | __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS);

        public FontAndColorProvider()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _storage = (IVsFontAndColorStorage)GetGlobalService(typeof(SVsFontAndColorStorage));
            Assumes.Present(_storage);
            ErrorHandler.ThrowOnFailure(_storage.OpenCategory(Constants.FontAndColorsCategoryGuid, _storageFlags));

            var fontAndColorService = (FontAndColorService)GetGlobalService(typeof(FontAndColorService));
            fontAndColorService.ItemsChanged += () =>
            {
                FontAndColorState = new FontAndColorState(this);
                FontAndColorInfoChanged?.Invoke();
            };

            FontAndColorState = new FontAndColorState(this);
        }

        public (Color fg, Color bg, bool bold) GetInfo(FontAndColorItem item) =>
            GetInfo(item.GetDisplayName());

        public (Color fg, Color bg, bool bold) GetHighlightInfo(DataHighlightColor highlight) =>
            GetInfo(highlight.GetDisplayName());

        public (Color fg, Color bg) GetColorInfo(FontAndColorItem item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var colorInfo = new ColorableItemInfo[1];
            ErrorHandler.ThrowOnFailure(_storage.GetItem(item.GetDisplayName(), colorInfo));

            var fg = FontAndColorService.ReadVsColor(colorInfo[0].crForeground);
            var bg = FontAndColorService.ReadVsColor(colorInfo[0].crBackground);

            return (fg, bg);
        }

        private (Color fg, Color bg, bool bold) GetInfo(string item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var colorInfo = new ColorableItemInfo[1];
            ErrorHandler.ThrowOnFailure(_storage.GetItem(item, colorInfo));

            var fg = FontAndColorService.ReadVsColor(colorInfo[0].crForeground);
            var bg = FontAndColorService.ReadVsColor(colorInfo[0].crBackground);
            var isBold = ((FONTFLAGS)colorInfo[0].dwFontFlags & FONTFLAGS.FF_BOLD) == FONTFLAGS.FF_BOLD;

            return (fg, bg, isBold);
        }

        public (Font font, Color foreground) GetInfo(FontAndColorItem item, Font fontPrototype)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var fontw = new LOGFONTW[1];
                var fontInfo = new FontInfo[1];
                ErrorHandler.ThrowOnFailure(_storage.GetFont(fontw, fontInfo));
                var colorInfo = new ColorableItemInfo[1];
                ErrorHandler.ThrowOnFailure(_storage.GetItem(item.GetDisplayName(), colorInfo));

                var fontName = fontInfo[0].bstrFaceName;
                var fontSize = fontInfo[0].wPointSize;
                var isBold = ((FONTFLAGS)colorInfo[0].dwFontFlags & FONTFLAGS.FF_BOLD) == FONTFLAGS.FF_BOLD;

                var font = new Font(fontName, fontSize, isBold ? FontStyle.Bold : FontStyle.Regular);
                var foregroundColor = ColorTranslator.FromWin32((int)colorInfo[0].crForeground);

                return (font, foregroundColor);
            }
            catch
            {
                return (fontPrototype, Color.Black);
            }
        }
    }
}
