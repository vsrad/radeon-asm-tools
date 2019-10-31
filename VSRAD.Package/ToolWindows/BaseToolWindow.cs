using Microsoft.VisualStudio.Shell;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VSRAD.Package.ToolWindows
{
    public abstract class BaseToolWindow : ToolWindowPane
    {
        private EnvDTE.WindowEvents WindowEvents { get; set; }

        private readonly UIElement _projectStateMissingMessage = new TextBlock
        {
            Text = "No active projects found.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        public BaseToolWindow(string caption) : base(null)
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
            var dte = (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
            var events = (EnvDTE80.Events2)dte.Events;
            WindowEvents = events.WindowEvents;
            WindowEvents.WindowActivated += OnWindowFocusLost;
        }

        protected virtual void OnWindowFocusLost() { }

        private void OnWindowFocusLost(EnvDTE.Window _, EnvDTE.Window lostFocus)
        {
            if (lostFocus == null) return;

            if (lostFocus.Caption == Caption)
            {
                OnWindowFocusLost();
            }
        }
    }
}
