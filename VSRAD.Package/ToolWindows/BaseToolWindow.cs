using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    public abstract class BaseToolWindow : ToolWindowPane
    {
        private EnvDTE.WindowEvents _windowEvents;
        private bool _dteWindowHasFocus;

        private readonly UIElement _projectStateMissingMessage = new TextBlock
        {
            Text = "No supported projects open.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        protected BaseToolWindow(string caption) : base(null)
        {
            Caption = caption;
            Content = new Grid
            {
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            OnProjectUnloaded();
        }

        protected abstract UIElement CreateToolControl(IToolWindowIntegration integration);

        public void BringToFront()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var window = VsShellUtilities.GetWindowObject((IVsWindowFrame)Frame);
            window.Activate(); // Use Activate() instead of IVsWindowFrame.Show() because the latter doesn't open the window if it's set to Auto Hide
        }

        public void OnProjectLoaded(IToolWindowIntegration integration)
        {
            ((Grid)Content).Children.Clear();
            ((Grid)Content).Children.Add(CreateToolControl(integration));
        }

        public void OnProjectUnloaded()
        {
            ((Grid)Content).Children.Clear();
            ((Grid)Content).Children.Add(_projectStateMissingMessage);
        }

        public override void OnToolWindowCreated()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = (EnvDTE80.DTE2)GetService(typeof(EnvDTE.DTE));
            _windowEvents = dte.Events.WindowEvents;
            _windowEvents.WindowActivated += OnDteWindowFocusChanged;
            Application.Current.Activated += (sender, e) => OnVsWindowFocusChanged(hasFocus: true);
            Application.Current.Deactivated += (sender, e) => OnVsWindowFocusChanged(hasFocus: false);
        }

        protected virtual void OnWindowFocusChanged(bool hasFocus) { }

        private void OnDteWindowFocusChanged(EnvDTE.Window gotFocus, EnvDTE.Window lostFocus)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (gotFocus?.Caption == Caption)
            {
                _dteWindowHasFocus = true;
                OnWindowFocusChanged(_dteWindowHasFocus);
            }
            else if (lostFocus?.Caption == Caption)
            {
                _dteWindowHasFocus = false;
                OnWindowFocusChanged(_dteWindowHasFocus);
            }
        }

        private void OnVsWindowFocusChanged(bool hasFocus)
        {
            if (_dteWindowHasFocus)
                OnWindowFocusChanged(hasFocus);
        }
    }
}
