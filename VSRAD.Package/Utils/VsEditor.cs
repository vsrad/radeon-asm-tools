using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.IO;

namespace VSRAD.Package.Utils
{
    public static class VsEditor
    {
        public static void OpenFileInEditor(SVsServiceProvider serviceProvider, string path, string lineMarker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var dte = serviceProvider.GetService(typeof(DTE)) as DTE;
                Assumes.Present(dte);
                dte.ItemOperations.OpenFile(path);

                if (string.IsNullOrEmpty(lineMarker))
                    return;

                var lineNumber = GetMarkedLineNumber(path, lineMarker);

                var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
                Assumes.Present(textManager);

                textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out var activeView);
                activeView.SetCaretPos(lineNumber, 0);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to open {path} in editor", e);
            }
        }

        private static int GetMarkedLineNumber(string file, string lineMarker)
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                if (line == lineMarker)
                    return lineNumber;
                ++lineNumber;
            }
            return 0;
        }
    }
}
