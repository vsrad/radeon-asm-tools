using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace VSRAD.Package.DebugVisualizer
{
    public enum FontAndColorItem
    {
        Header, WatchNames, ColumnSeparator, HiddenColumnSeparator
    }

    static class FontAndColorItems
    {
        public static string GetDisplayName(this FontAndColorItem item)
        {
            switch (item)
            {
                case FontAndColorItem.Header: return "Header";
                case FontAndColorItem.WatchNames: return "Watch Names";
                case FontAndColorItem.ColumnSeparator: return "Column Separator";
                case FontAndColorItem.HiddenColumnSeparator: return "Hidden Column Separator";
            }
            throw new NotImplementedException();
        }

        public static string GetDisplayName(this DataHighlightColor item)
        {
            switch (item)
            {
                case DataHighlightColor.None: return "Data";
                case DataHighlightColor.ColumnRed: return "Data - Column Red Highlight";
                case DataHighlightColor.ColumnGreen: return "Data - Column Green Highlight";
                case DataHighlightColor.ColumnBlue: return "Data - Column Blue Highlight";
                case DataHighlightColor.RowRed: return "Data - Row Red Highlight";
                case DataHighlightColor.RowGreen: return "Data - Row Green Highlight";
                case DataHighlightColor.RowBlue: return "Data - Row Blue Highlight";
                case DataHighlightColor.Inactive: return "Data - Inactive";
            }
            throw new NotImplementedException();
        }
    }

    [Guid(Constants.FontAndColorDefaultsServiceId)]
    sealed class FontAndColorService : IVsFontAndColorDefaults, IVsFontAndColorDefaultsProvider, IVsFontAndColorEvents
    {
        public event Action ItemsChanged;
        private bool _itemsChangedBeforeApply = false;

        private const string _defaultFontName = "Consolas";
        private const ushort _defaultFontSize = 10;

        private static readonly List<AllColorableItemInfo> _items = new List<AllColorableItemInfo>()
        {
            CreateItem(FontAndColorItem.Header.GetDisplayName()),
            CreateItem(FontAndColorItem.WatchNames.GetDisplayName()),
            CreateItem(FontAndColorItem.ColumnSeparator.GetDisplayName(), bg: Color.FromArgb(0xa0a0a0), hasText: false),
            CreateItem(FontAndColorItem.HiddenColumnSeparator.GetDisplayName(), bg: Color.FromArgb(0x404040), hasText: false),
            CreateItem(DataHighlightColor.None.GetDisplayName()),
            CreateItem(DataHighlightColor.Inactive.GetDisplayName(), bg: Color.LightGray, hasText: false),
            CreateItem(DataHighlightColor.ColumnRed.GetDisplayName(), bg: Color.FromArgb(245, 226, 227)),
            CreateItem(DataHighlightColor.ColumnGreen.GetDisplayName(), bg: Color.FromArgb(227, 245, 226)),
            CreateItem(DataHighlightColor.ColumnBlue.GetDisplayName(), bg: Color.FromArgb(226, 230, 245)),
            CreateItem(DataHighlightColor.RowRed.GetDisplayName(), fg: Color.Red, bg: Color.Empty),
            CreateItem(DataHighlightColor.RowGreen.GetDisplayName(), fg: Color.Green, bg: Color.Empty),
            CreateItem(DataHighlightColor.RowBlue.GetDisplayName(), fg: Color.Blue, bg: Color.Empty),
        };

        // Changes to ProvideFontAndColorsCategory will not be registered until this method is run.
        // This is only useful when developing the extension, so make sure to guard this call with #if DEBUG.
        internal static void ClearFontAndColorCache(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            IVsFontAndColorCacheManager cacheManager = (IVsFontAndColorCacheManager)
                serviceProvider.GetService(typeof(SVsFontAndColorCacheManager));
            Assumes.Present(cacheManager);
            var categoryGuid = Constants.FontAndColorsCategoryGuid;
            cacheManager.ClearCache(ref categoryGuid);
        }

        public static Color ReadVsColor(uint vsColor) => vsColor == 0xffffffff ? Color.Empty : ColorTranslator.FromWin32((int)vsColor);

        public static uint MakeVsColor(Color color) => color == Color.Empty ? 0xffffffff : (uint)ColorTranslator.ToWin32(color);

        private static AllColorableItemInfo CreateItem(string name, Color? fg = null, Color? bg = null, bool hasText = true)
        {
            var fgRaw = MakeVsColor(fg ?? Color.Black);
            var bgRaw = MakeVsColor(bg ?? Color.White);

            var flags = __FCITEMFLAGS.FCIF_ALLOWBGCHANGE | __FCITEMFLAGS.FCIF_ALLOWCUSTOMCOLORS;
            if (hasText)
                flags |= __FCITEMFLAGS.FCIF_ALLOWFGCHANGE | __FCITEMFLAGS.FCIF_ALLOWBOLDCHANGE;

            return new AllColorableItemInfo
            {
                bFlagsValid = 1,
                fFlags = (uint)flags,
                bNameValid = 1,
                bstrName = name,
                bLocalizedNameValid = 1,
                bstrLocalizedName = name,
                Info = new ColorableItemInfo
                {
                    bFontFlagsValid = 1,
                    dwFontFlags = 0,
                    bForegroundValid = 1,
                    crForeground = (uint)__VSCOLORTYPE.CT_RAW | fgRaw,
                    bBackgroundValid = 1,
                    crBackground = (uint)__VSCOLORTYPE.CT_RAW | bgRaw,
                }
            };
        }

        int IVsFontAndColorDefaultsProvider.GetObject(ref Guid rguidCategory, out object ppObj)
        {
            rguidCategory = Constants.FontAndColorsCategoryGuid;
            ppObj = this;
            return VSConstants.S_OK;
        }

        int IVsFontAndColorDefaults.GetCategoryName(out string pbstrName)
        {
            pbstrName = Constants.FontAndColorsCategoryTitle;
            return VSConstants.S_OK;
        }

        int IVsFontAndColorDefaults.GetBaseCategory(out Guid pguidBase)
        {
            pguidBase = Constants.FontAndColorsCategoryGuid;
            return VSConstants.S_OK;
        }

        int IVsFontAndColorDefaults.GetItemCount(out int pcItems)
        {
            pcItems = _items.Count;
            return VSConstants.S_OK;
        }

        int IVsFontAndColorDefaults.GetItem(int iItem, AllColorableItemInfo[] pInfo)
        {
            pInfo[0] = _items[iItem];
            return VSConstants.S_OK;
        }

        int IVsFontAndColorDefaults.GetItemByName(string szItem, AllColorableItemInfo[] pInfo)
        {
            pInfo[0] = _items.FirstOrDefault(i => i.bstrName == szItem);
            return VSConstants.S_OK;
        }

        int IVsFontAndColorDefaults.GetFont(FontInfo[] pInfo)
        {
            pInfo[0] = new FontInfo
            {
                bFaceNameValid = 1,
                bstrFaceName = _defaultFontName,
                bPointSizeValid = 1,
                wPointSize = _defaultFontSize
            };
            return VSConstants.S_OK;
        }

        int IVsFontAndColorDefaults.GetFlags(out uint dwFlags)
        {
            dwFlags = 0;
            return VSConstants.S_OK;
        }

        int IVsFontAndColorDefaults.GetPriority(out ushort pPriority)
        {
            pPriority = 0;
            return VSConstants.E_NOTIMPL;
        }

        int IVsFontAndColorEvents.OnFontChanged(ref Guid category, FontInfo[] _1, LOGFONTW[] _2, uint _3) => OnChange(ref category);

        int IVsFontAndColorEvents.OnItemChanged(ref Guid category, string _1, int _2, ColorableItemInfo[] _3, uint _4, uint _5) => OnChange(ref category);

        int IVsFontAndColorEvents.OnReset(ref Guid category) => OnChange(ref category);

        int IVsFontAndColorEvents.OnResetToBaseCategory(ref Guid category) => OnChange(ref category);

        int IVsFontAndColorEvents.OnApply()
        {
            // Don't fire multiple events if more than one item is changed
            if (_itemsChangedBeforeApply)
                ItemsChanged?.Invoke();
            _itemsChangedBeforeApply = false;
            return VSConstants.S_OK;
        }

        private int OnChange(ref Guid category)
        {
            _itemsChangedBeforeApply = _itemsChangedBeforeApply || category == Constants.FontAndColorsCategoryGuid;
            return VSConstants.S_OK;
        }
    }
}
