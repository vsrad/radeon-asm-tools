using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

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

        private static readonly Type _sVsWindowManagerType = Type.GetType("Microsoft.Internal.VisualStudio.Shell.Interop.SVsWindowManager, Microsoft.VisualStudio.Platform.WindowManagement");
        private static readonly Type _viewManagerType = Type.GetType("Microsoft.VisualStudio.PlatformUI.Shell.ViewManager, Microsoft.VisualStudio.Shell.ViewManager");
        private static readonly Type _viewDockOperationsType = Type.GetType("Microsoft.VisualStudio.PlatformUI.Shell.DockOperations, Microsoft.VisualStudio.Shell.ViewManager");

        public static void OpenFileInEditor(SVsServiceProvider serviceProvider, string path, string lineMarker, bool forceOppositeTab, bool preserveActiveDoc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var windowManager = serviceProvider.GetService(_sVsWindowManagerType);
                Assumes.Present(windowManager);
                // Using GetType() because _sVsWindowManagerType is just an interface; ActiveDocumentFrame is present only in the actual implementation class
                dynamic originalDocumentFrame = windowManager.GetType().GetProperty("ActiveDocumentFrame", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(windowManager);

                var vsUIShellOpenDocument = serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
                Assumes.Present(vsUIShellOpenDocument);
                ErrorHandler.ThrowOnFailure(vsUIShellOpenDocument.OpenDocumentViaProject(path, Guid.Empty, out _, out _, out _, out var newDocumentFrame));

                if (!string.IsNullOrEmpty(lineMarker))
                    SetCaretAtLineMarker(newDocumentFrame, lineMarker);

                if (preserveActiveDoc)
                    ErrorHandler.ThrowOnFailure(newDocumentFrame.ShowNoActivate());
                else
                    ErrorHandler.ThrowOnFailure(newDocumentFrame.Show());

                if (forceOppositeTab && newDocumentFrame != originalDocumentFrame)
                    MoveToOppositeTab(newDocumentFrame, originalDocumentFrame, preserveActiveDoc);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to open {path} in editor", e);
            }
        }

        private static void MoveToOppositeTab(IVsWindowFrame newDocumentFrame, dynamic originalDocumentFrame, bool preserveActiveDoc)
        {
            dynamic newDocumentFrameView = ((dynamic)newDocumentFrame).FrameView;
            dynamic newDocumentTabGroup = newDocumentFrameView.Parent;

            // if the document is in the same tab group as the original active document, move it
            if (originalDocumentFrame.FrameView.Parent == newDocumentTabGroup)
            {
                // If there's only one existing tab group, create a new one
                var tabGroups = (IList)newDocumentTabGroup.Parent.VisibleChildren;
                if (tabGroups.Count == 1)
                {
                    var viewManager = _viewManagerType.GetProperty("Instance").GetValue(null);
                    var createDocumentGroup = _viewManagerType.GetMethod("CreateDocumentGroup", BindingFlags.NonPublic | BindingFlags.Instance);
                    createDocumentGroup.Invoke(viewManager, new[] { newDocumentFrameView, 0 });
                }

                // Choose the tab group the document should be assigned to
                int tabGroupIndex = tabGroups.IndexOf(newDocumentTabGroup);
                dynamic newTabGroup;
                if (tabGroupIndex + 1 < tabGroups.Count)
                    newTabGroup = tabGroups[tabGroupIndex + 1];
                else
                    newTabGroup = tabGroups[tabGroupIndex - 1];

                // Remove the document from the tab group it was assigned to
                newDocumentFrameView.Detach();

                // Assign the document to the new tab group
                _viewDockOperationsType.GetMethod("Dock").Invoke(null, new[] { newTabGroup, newDocumentFrameView, 0 });

                if (preserveActiveDoc) // Docking operations may cause the original document to lose focus
                    originalDocumentFrame.GetType().GetMethod("RestoreFocusNextLoad", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(originalDocumentFrame, null);
            }
            // Otherwise, make the document active in its tab group without changing the global active document
            else
            {
                newDocumentTabGroup.SelectedElement = newDocumentFrameView;
            }
        }

        private static void SetCaretAtLineMarker(IVsWindowFrame documentFrame, string lineMarker)
        {
            var vsTextView = VsShellUtilities.GetTextView(documentFrame);
            ErrorHandler.ThrowOnFailure(vsTextView.GetBuffer(out var vsTextLines));
            ErrorHandler.ThrowOnFailure(vsTextLines.GetLineCount(out var numLines));
            for (int i = 0; i < numLines; ++i)
            {
                ErrorHandler.ThrowOnFailure(vsTextLines.GetLengthOfLine(i, out var lineLength));
                if (lineLength != lineMarker.Length)
                    continue;

                ErrorHandler.ThrowOnFailure(vsTextLines.GetLineText(i, 0, i, lineLength, out var line));
                if (line != lineMarker)
                    continue;

                ErrorHandler.ThrowOnFailure(vsTextView.SetCaretPos(i, 0));
                break;
            }
        }
    }
}
