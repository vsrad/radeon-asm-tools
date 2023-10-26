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

    public enum HeatmapColor
    {
        Cold, Mean, Hot
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
                case DataHighlightColor.Inactive: return "Data - Inactive";
                case DataHighlightColor.Red: return "Data - Red Highlight";
                case DataHighlightColor.Green: return "Data - Green Highlight";
                case DataHighlightColor.Blue: return "Data - Blue Highlight";
            }
            throw new NotImplementedException();
        }

        public static string GetDisplayName(this HeatmapColor item)
        {
            switch (item)
            {
                case HeatmapColor.Cold: return "Heatmap - Cold Color";
                case HeatmapColor.Mean: return "Heatmap - Mean Color";
                case HeatmapColor.Hot: return "Heatmap - Hot Color";
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
            CreateItem(DataHighlightColor.Red.GetDisplayName(), fg: Color.Red, bg: Color.FromArgb(245, 226, 227)),
            CreateItem(DataHighlightColor.Green.GetDisplayName(), fg: Color.Green, bg: Color.FromArgb(227, 245, 226)),
            CreateItem(DataHighlightColor.Blue.GetDisplayName(), fg: Color.Blue, bg: Color.FromArgb(226, 230, 245)),
            CreateItem(HeatmapColor.Cold.GetDisplayName(), bg: Color.FromArgb(162, 201, 229)),
            CreateItem(HeatmapColor.Mean.GetDisplayName(), bg: Color.FromArgb(255, 255, 255)),
            CreateItem(HeatmapColor.Hot.GetDisplayName(), bg: Color.FromArgb(215, 145, 132)),
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

        public static Color ReadVsColor(uint vsColor) => ColorTranslator.FromWin32((int)vsColor);

        public static uint MakeVsColor(Color color) => (uint)ColorTranslator.ToWin32(color);

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

#if VS2019
        int IVsFontAndColorEvents.OnFontChanged(ref Guid category, FontInfo[] _1, LOGFONTW[] _2, uint _3) => OnChange(ref category);

        int IVsFontAndColorEvents.OnItemChanged(ref Guid category, string _1, int _2, ColorableItemInfo[] _3, uint _4, uint _5) => OnChange(ref category);
#else
        int IVsFontAndColorEvents.OnFontChanged(ref Guid category, FontInfo[] pInfo, LOGFONTW[] pLOGFONT, IntPtr HFONT) => OnChange(ref category);

        int IVsFontAndColorEvents.OnItemChanged(ref Guid category, string szItem, int iItem, ColorableItemInfo[] pInfo, uint crLiteralForeground, uint crLiteralBackground) => OnChange(ref category);
#endif

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
