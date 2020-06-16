using Microsoft.VisualStudio.Shell;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    public abstract class BaseToolWindow : ToolWindowPane
    {
        private EnvDTE.WindowEvents _windowEvents;

        private readonly UIElement _projectStateMissingMessage = new TextBlock
        {
            Text = "No active projects found.",
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
            var dte = (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
            _windowEvents = dte.Events.WindowEvents;
            _windowEvents.WindowActivated += OnWindowFocusChanged;
        }

        protected virtual void OnWindowFocusChanged(bool hasFocus) { }

        private void OnWindowFocusChanged(EnvDTE.Window gotFocus, EnvDTE.Window lostFocus)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (gotFocus?.Caption == Caption)
                OnWindowFocusChanged(hasFocus: true);
            else if (lostFocus?.Caption == Caption)
                OnWindowFocusChanged(hasFocus: false);
        }
    }
}
