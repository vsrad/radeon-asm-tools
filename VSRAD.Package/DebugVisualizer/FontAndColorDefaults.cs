using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VSRAD.Package.DebugVisualizer
{
    [Guid(Constants.FontAndColorDefaultsServiceId)]
    sealed class FontAndColorDefaults : IVsFontAndColorDefaults, IVsFontAndColorDefaultsProvider
    {
        private readonly List<AllColorableItemInfo> _items = new List<AllColorableItemInfo>()
        {
            CreateItem("Header"),
            CreateItem("Data"),
            CreateItem("Watch names")
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

        private static AllColorableItemInfo CreateItem(string name)
        {
            return new AllColorableItemInfo
            {
                bFlagsValid = 1,
                fFlags = (uint)(__FCITEMFLAGS.FCIF_ALLOWBOLDCHANGE | __FCITEMFLAGS.FCIF_ALLOWFGCHANGE | __FCITEMFLAGS.FCIF_ALLOWCUSTOMCOLORS),
                bNameValid = 1,
                bstrName = name,
                bLocalizedNameValid = 1,
                bstrLocalizedName = name,
                Info = new ColorableItemInfo
                {
                    bFontFlagsValid = 1,
                    dwFontFlags = 0,
                    bForegroundValid = 1,
                    crForeground = (uint)__VSCOLORTYPE.CT_RAW | 0x0,
                    bBackgroundValid = 1,
                    crBackground = (uint)__VSCOLORTYPE.CT_RAW | 0x00ffffff,
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

        int IVsFontAndColorDefaults.GetItemByName(string szItem, AllColorableItemInfo[] pInfo) => VSConstants.E_NOTIMPL;

        int IVsFontAndColorDefaults.GetFont(FontInfo[] pInfo) => VSConstants.S_OK;

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
    }
}
