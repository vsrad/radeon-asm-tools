using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    [Guid(Constants.NavigationListToolWindowPaneGuid)]
    public class NavigationList : ToolWindowPane
    {
        private static NavigationListControl Control;

        public NavigationList() : base(null)
        {
            Caption = "Radeon Asm Navigation List";
        }

        protected override void Initialize()
        {
            Control = new NavigationListControl();
            Content = Control;
        }

        public static void UpdateNavigationList(IReadOnlyList<NavigationToken> tokens)
        {
            if (Control != null) UpdateNavigationList(Control, tokens);
            if (Syntax.Package.Instance == null) return;

            var window = (NavigationList)Syntax.Package.Instance.FindToolWindow(typeof(NavigationList), 0, true);
            if ((null == window) || (null == window.Frame)) return;

            if (window.Content is NavigationListControl navigationListControl)
            {
                var windowFrame = (IVsWindowFrame)window.Frame;
                ErrorHandler.ThrowOnFailure(windowFrame.Show());
                UpdateNavigationList(navigationListControl, tokens);
            }
        }

        private static void UpdateNavigationList(NavigationListControl control, IReadOnlyList<NavigationToken> tokens) =>
            ThreadHelper.JoinableTaskFactory.RunAsync(() => control.UpdateNavigationListAsync(tokens));
    }
}
