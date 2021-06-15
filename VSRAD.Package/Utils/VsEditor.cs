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

        public static void OpenFileInEditor(SVsServiceProvider serviceProvider, string path, string lineMarker,
            bool forceOppositeTab, bool preserveActiveDoc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var dte = serviceProvider.GetService(typeof(DTE)) as DTE;
                Assumes.Present(dte);

                // HACK: VS have some issues with saving focus
                // on the initial active doc after execution of
                // commands, that manipulate with tab groups.
                // However, toggling pin state of active tab
                // seems to solve this problem.
                if (preserveActiveDoc)
                {
                    dte.ExecuteCommand("Window.PinTab");
                    dte.ExecuteCommand("Window.PinTab");
                }

                var orgNext = dte.Commands.Item("Window.MovetoNextTabGroup").IsAvailable;
                var orgPrev = dte.Commands.Item("Window.MovetoPreviousTabGroup").IsAvailable;

                var orgActiveFile = dte.ActiveDocument;

                if (orgActiveFile.FullName != path)
                {
                    dte.ExecuteCommand("File.OpenFile", path);
                    if (forceOppositeTab)
                    {
                        var curNext = dte.Commands.Item("Window.MovetoNextTabGroup").IsAvailable;
                        var curPrev = dte.Commands.Item("Window.MovetoPreviousTabGroup").IsAvailable;

                        if (curNext == orgNext && curPrev == orgPrev)
                        {
                            if (curNext)
                                dte.ExecuteCommand("Window.MovetoNextTabGroup");
                            else if (curPrev)
                                dte.ExecuteCommand("Window.MovetoPreviousTabGroup");
                            else if (dte.Commands.Item("Window.NewVerticalTabGroup").IsAvailable)
                                dte.ExecuteCommand("Window.NewVerticalTabGroup");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(lineMarker))
                {
                    var lineNumber = GetMarkedLineNumber(path, lineMarker);

                    var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
                    Assumes.Present(textManager);

                    textManager.GetActiveView2(0, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out var activeView);
                    activeView.SetCaretPos(lineNumber, 0);
                }

                if (preserveActiveDoc)
                    dte.ExecuteCommand("File.OpenFile", orgActiveFile.FullName);
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
