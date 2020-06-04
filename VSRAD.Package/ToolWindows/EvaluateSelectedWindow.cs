using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    [Guid("8C576A78-91BD-46F0-A6EA-3D2A3DD9DB4D")]
    public sealed class EvaluateSelectedWindow : ToolWindowPane
    {
        public EvaluateSelectedControl EvaluateSelectedControl { get; private set; }
        public EvaluateSelectedWindow()
        {
            Caption = "RAD Evaluate selected";
            Content = new Grid
            {
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            OnProjectUnloaded();
        }

        public void OnProjectLoaded(IToolWindowIntegration integration)
        {
            this.EvaluateSelectedControl = new EvaluateSelectedControl(integration);
            ((Grid)Content).Children.Clear();
            ((Grid)Content).Children.Add(EvaluateSelectedControl);
        }

        public void OnProjectUnloaded()
        {
            ((Grid)Content).Children.Clear();
            ((Grid)Content).Children.Add(new TextBlock
            {
                Text = "No active projects found.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
    }
}
