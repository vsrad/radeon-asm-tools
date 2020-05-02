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
        (Color fg, Color bg, bool bold) GetHighlightInfo(DataHighlightColor highlight);
    }

    sealed class FontAndColorProvider : IFontAndColorProvider
    {
        public event Action FontAndColorInfoChanged;

        private readonly IVsFontAndColorStorage _storage;
        private const uint _storageFlags = (uint)(__FCSTORAGEFLAGS.FCSF_LOADDEFAULTS
            | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES
            | __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS);

        private DataFontAndColor _cachedColumnFontAndColor;
        public DataFontAndColor ColumnFontAndColor
        {
            get
            {
                if (_cachedColumnFontAndColor == null)
                    _cachedColumnFontAndColor = new DataFontAndColor(this);
                return _cachedColumnFontAndColor;
            }
        }

        public FontAndColorProvider()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _storage = (IVsFontAndColorStorage)GetGlobalService(typeof(SVsFontAndColorStorage));
            Assumes.Present(_storage);

            var fontAndColorService = (FontAndColorService)GetGlobalService(typeof(FontAndColorService));
            fontAndColorService.ItemsChanged += () =>
            {
                _cachedColumnFontAndColor = null;
                FontAndColorInfoChanged?.Invoke();
            };

            ErrorHandler.ThrowOnFailure(_storage.OpenCategory(Constants.FontAndColorsCategoryGuid, _storageFlags));
        }

        public (Color fg, Color bg, bool bold) GetHighlightInfo(DataHighlightColor highlight)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var colorInfo = new ColorableItemInfo[1];
            ErrorHandler.ThrowOnFailure(_storage.GetItem(highlight.GetDisplayName(), colorInfo));

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
