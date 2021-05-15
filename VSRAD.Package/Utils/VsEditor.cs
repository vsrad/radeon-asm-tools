using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.Shell;
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

            var dte = serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);

            dte.ItemOperations.OpenFile(documentPath);

            var document = (TextDocument)dte.ActiveDocument.Object("TextDocument");
            var editPoint = document.StartPoint.CreateEditPoint();
            editPoint.MoveToLineAndOffset((int)line + 1, 1);
            editPoint.TryToShow(vsPaneShowHow.vsPaneShowCentered);
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
                var dte = serviceProvider.GetService(typeof(DTE)) as DTE;
                Assumes.Present(dte);
                var activeFile = dte.ActiveDocument;
                dte.ItemOperations.OpenFile(path); // open requested file in VS editor

                if (!string.IsNullOrEmpty(lineMarker))
                {
                    var lineNumber = GetMarkedLineNumber(path, lineMarker);

                    var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
                    Assumes.Present(textManager);

                    textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out var activeView);
                    activeView.SetCaretPos(lineNumber, 0);
                }

                dte.ItemOperations.OpenFile(activeFile.FullName); // preserving old active document
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
