using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    [Guid(Constants.NavigationListToolWindowPaneGuid)]
    public class NavigationList : ToolWindowPane
    {
        public NavigationList() : base(null)
        {
            Caption = "Radeon Asm Navigation List";
        }

        protected override void Initialize()
        {
            var navigationTokenService = Syntax.Package.Instance.GetMEFComponent<INavigationTokenService>();
            Content = new NavigationListControl(navigationTokenService);
        }

        public static Task UpdateNavigationListAsync(IReadOnlyList<NavigationToken> tokens)
        {
            if (Syntax.Package.Instance == null)
                return Task.CompletedTask;

            var window = (NavigationList)Syntax.Package.Instance.FindToolWindow(typeof(NavigationList), 0, true);
            if ((null == window) || (null == window.Frame))
                return Task.CompletedTask;

            if (window.Content is NavigationListControl navigationListControl)
            {
                var windowFrame = (IVsWindowFrame)window.Frame;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
                return navigationListControl.UpdateNavigationListAsync(tokens);
            }

            return Task.CompletedTask;
        }
    }
}
