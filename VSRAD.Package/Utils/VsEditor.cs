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
        public static IEnumerable<IVsTextLineMarker> GetTextLineMarkersOfType(IVsTextBuffer textBuffer, int type)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ((IVsTextLines)textBuffer).EnumMarkers(0, 0, 0, 0, type, (uint)ENUMMARKERFLAGS.EM_ENTIREBUFFER, out var markersEnum);
            markersEnum.GetCount(out int markerCount);
            var markers = new IVsTextLineMarker[markerCount];
            for (int i = 0; i < markerCount; ++i)
                markersEnum.Next(out markers[i]);
            return markers;
        }

#if VS2019
        private static readonly Type _sVsWindowManagerType = Type.GetType("Microsoft.Internal.VisualStudio.Shell.Interop.SVsWindowManager, Microsoft.VisualStudio.Platform.WindowManagement");
#else
        private static readonly Type _sVsWindowManagerType = Type.GetType("Microsoft.Internal.VisualStudio.Shell.Interop.SVsWindowManager, Microsoft.VisualStudio.Interop");
#endif
        private static readonly Type _viewManagerType = Type.GetType("Microsoft.VisualStudio.PlatformUI.Shell.ViewManager, Microsoft.VisualStudio.Shell.ViewManager");
        private static readonly Type _viewDockOperationsType = Type.GetType("Microsoft.VisualStudio.PlatformUI.Shell.DockOperations, Microsoft.VisualStudio.Shell.ViewManager");

        public static void OpenFileInEditor(SVsServiceProvider serviceProvider, string path, uint? line, string lineMarker, bool forceOppositeTab, bool preserveActiveDoc)
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

                if (line is uint caretLine)
                {
                    var textView = VsShellUtilities.GetTextView(newDocumentFrame);
                    ErrorHandler.ThrowOnFailure(textView.SetCaretPos((int)caretLine, 0));
                    ErrorHandler.ThrowOnFailure(textView.CenterLines((int)caretLine, 1));
                }
                else if (!string.IsNullOrEmpty(lineMarker))
                {
                    SetCaretAtLineMarker(newDocumentFrame, lineMarker);
                }

                if (forceOppositeTab && originalDocumentFrame != null && originalDocumentFrame != newDocumentFrame)
                    MoveToOppositeTab(newDocumentFrame, originalDocumentFrame, preserveActiveDoc);

                if (preserveActiveDoc)
                    ErrorHandler.ThrowOnFailure(newDocumentFrame.ShowNoActivate());
                else
                    ErrorHandler.ThrowOnFailure(newDocumentFrame.Show());
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

                if (preserveActiveDoc)
                {
                    // There's a race condition in Visual Studio 2019 which makes the new document frame steal focus from the original document
                    // _after_ we exit from OpenFileInEditor, caused by WindowFrame using the FrameworkElement.Loaded event to move focus into itself.
                    // The hack below listens to this event and restores focus to the original document frame.
                    // This happens only when performing docking operations. To reproduce the bug:
                    // 1. Create an action that opens a file in the editor, enable forceOppositeTab and preserveActiveDoc.
                    // 2. Open a different source file in the editor, which becomes the active (or "original") document. There should be a single tab group in the editor at this point.
                    // 3. Run the action once. A tab group will be created on the right with the new document.
                    // 4. Move the new document to the left tab group and switch to the original document's tab. There should be a single tab group in the editor at this point.
                    // 5. Run the action again. A tab group will be created on the right and the new document will receive focus despite preserveActiveDoc.
                    // Repeat steps 4-5 if you can't reproduce it at first try.
                    var innerContentProperty = newDocumentFrame.GetType().GetProperty("InnerContent", BindingFlags.NonPublic | BindingFlags.Instance);
                    var newFrameContent = (System.Windows.FrameworkElement)innerContentProperty.GetValue(newDocumentFrame);
                    var originalFrameContent = (System.Windows.FrameworkElement)innerContentProperty.GetValue(originalDocumentFrame);
                    void NewDocumentFrameLoaded(object sender, System.Windows.RoutedEventArgs e)
                    {
                        newFrameContent.Loaded -= NewDocumentFrameLoaded;
                        var originalFrameRoot = System.Windows.PresentationSource.FromVisual(originalFrameContent).RootVisual;
                        System.Windows.Input.FocusManager.SetFocusedElement(originalFrameRoot, null);
                        System.Windows.Input.Keyboard.Focus((System.Windows.IInputElement)originalFrameRoot);
                        Microsoft.VisualStudio.PlatformUI.FocusHelper.MoveFocusInto(originalFrameContent);
                    }
                    newFrameContent.Loaded += NewDocumentFrameLoaded;
                }
                
                newTabGroup.SelectedElement = newDocumentFrameView;
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
