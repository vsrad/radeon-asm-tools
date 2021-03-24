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
        private static NavigationListControl _control;

        public NavigationList() : base(null)
        {
            Caption = "Radeon Asm Navigation List";
        }

        protected override void Initialize()
        {
            _control = new NavigationListControl();
            Content = _control;
        }

        public static void UpdateNavigationList(IReadOnlyList<INavigationToken> tokens)
        {
            if (Syntax.Package.Instance == null) return;

            var window = (NavigationList)Syntax.Package.Instance.FindToolWindow(typeof(NavigationList), 0, true);
            if (window?.Frame == null) return;

            var windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            UpdateNavigationList(_control, tokens);
        }

        private static void UpdateNavigationList(NavigationListControl control, IReadOnlyList<INavigationToken> tokens) =>
            ThreadHelper.JoinableTaskFactory.RunAsync(() => control.UpdateNavigationListAsync(tokens));
    }
}
