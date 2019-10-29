using Microsoft.VisualStudio.Shell;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VSRAD.Package.ToolWindows
{
    public abstract class BaseToolWindow : ToolWindowPane
    {
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
    }
}
