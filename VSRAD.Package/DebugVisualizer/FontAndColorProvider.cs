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
    sealed class FontAndColorProvider
    {
        public readonly struct FontAndColorInfo
        {
            public string FontName { get; }
            public Color ForegroundColor { get; }
            public bool Bold { get; }

            public FontAndColorInfo(string fontName = null, Color? foregroundColor = null, bool bold = false)
            {
                FontName = fontName ?? FontAndColorService.DefaultFontName;
                ForegroundColor = foregroundColor ?? Color.Black;
                Bold = bold;
            }
        }

        public event Action FontAndColorInfoChanged;

        private readonly IVsFontAndColorStorage _storage;
        private const uint _storageFlags = (uint)(__FCSTORAGEFLAGS.FCSF_LOADDEFAULTS
            | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES
            | __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS);

        public FontAndColorProvider()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _storage = (IVsFontAndColorStorage)GetGlobalService(typeof(SVsFontAndColorStorage));
            Assumes.Present(_storage);

            var fontAndColorService = (FontAndColorService)GetGlobalService(typeof(FontAndColorService));
            fontAndColorService.ItemsChanged += () => FontAndColorInfoChanged?.Invoke();

            ErrorHandler.ThrowOnFailure(_storage.OpenCategory(Constants.FontAndColorsCategoryGuid, _storageFlags));
        }

        public FontAndColorInfo GetInfo(FontAndColorItem item)
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
                var foregroundColor = ColorTranslator.FromWin32((int)colorInfo[0].crForeground);
                var isBold = ((FONTFLAGS)colorInfo[0].dwFontFlags & FONTFLAGS.FF_BOLD) == FONTFLAGS.FF_BOLD;

                return new FontAndColorInfo(fontName, foregroundColor, isBold);
            }
            catch
            {
                return new FontAndColorInfo();
            }
        }
    }
}
