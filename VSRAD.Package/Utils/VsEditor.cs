using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.IO;

namespace VSRAD.Package.Utils
{
    public static class VsEditor
    {
        public static void NavigateToFileAndLine(IServiceProvider serviceProvider, string documentPath, uint line)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vsUIShellOpenDocument = serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            Assumes.Present(vsUIShellOpenDocument);

            var logicalView = Guid.Empty;
            ErrorHandler.ThrowOnFailure(vsUIShellOpenDocument.OpenDocumentViaProject(documentPath, ref logicalView, out _, out _, out _, out var windowFrame));

            windowFrame.Show();

            var vsTextView = VsShellUtilities.GetTextView(windowFrame);
            vsTextView.CenterLines((int)line, 0);
        }

        public static IEnumerable<IVsTextLineMarker> GetLineMarkersOfTypeInActiveView(IServiceProvider serviceProvider, int type)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            Assumes.Present(textManager);

            textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out var activeView);
            activeView.GetBuffer(out var linesBuffer);
            linesBuffer.EnumMarkers(0, 0, 0, 0, type, (uint)ENUMMARKERFLAGS.EM_ENTIREBUFFER, out var markersEnum);

            markersEnum.GetCount(out int markerCount);
            var markers = new IVsTextLineMarker[markerCount];
            for (int i = 0; i < markerCount; ++i)
            {
                markersEnum.Next(out var m);
                markers[i] = m;
            }
            return markers;
        }

        public static void OpenFileInEditor(SVsServiceProvider serviceProvider, string path, string lineMarker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var vsUIShellOpenDocument = serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
                Assumes.Present(vsUIShellOpenDocument);

                var logicalView = Guid.Empty;
                ErrorHandler.ThrowOnFailure(vsUIShellOpenDocument.OpenDocumentViaProject(path, ref logicalView, out _, out _, out _, out var windowFrame));

                // Force VS to refresh document contents
                var vsTextView = VsShellUtilities.GetTextView(windowFrame);
                ErrorHandler.ThrowOnFailure(vsTextView.GetBuffer(out var vsTextLines));
                ErrorHandler.ThrowOnFailure(vsTextLines.Reload(1));

                if (!string.IsNullOrEmpty(lineMarker))
                {
                    var lineNumber = GetMarkedLineNumber(path, lineMarker);
                    vsTextView.SetCaretPos(lineNumber, 0);
                }

                ErrorHandler.ThrowOnFailure(windowFrame.ShowNoActivate());
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
