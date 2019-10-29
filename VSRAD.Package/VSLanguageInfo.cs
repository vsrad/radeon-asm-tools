using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Runtime.InteropServices;

namespace VSRAD.Package
{
    [Guid(Deborgar.Constants.LanguageId)]
    internal class VSLanguageInfo : IVsLanguageInfo
    {
        int IVsLanguageInfo.GetLanguageName(out string bstrName)
        {
            bstrName = Deborgar.Constants.LanguageName;
            return VSConstants.S_OK;
        }

        int IVsLanguageInfo.GetFileExtensions(out string pbstrExtensions)
        {
            pbstrExtensions = "";
            return VSConstants.S_OK;
        }

        int IVsLanguageInfo.GetColorizer(IVsTextLines pBuffer, out IVsColorizer ppColorizer)
        {
            ppColorizer = null;
            return VSConstants.E_NOTIMPL;
        }

        int IVsLanguageInfo.GetCodeWindowManager(IVsCodeWindow pCodeWin, out IVsCodeWindowManager ppCodeWinMgr)
        {
            ppCodeWinMgr = null;
            return VSConstants.E_NOTIMPL;
        }
    }
}
