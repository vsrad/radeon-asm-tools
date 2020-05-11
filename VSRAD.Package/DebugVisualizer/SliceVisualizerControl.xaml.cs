using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using VSRAD.Package.Options;
using VSRAD.Package.ToolWindows;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    /// <summary>
    /// Interaction logic for SliceVisualizerControl.xaml
    /// </summary>
    public partial class SliceVisualizerControl : UserControl
    {
        public sealed class Context : DefaultNotifyPropertyChanged
        {
            public ProjectOptions Options { get; }
            public IReadOnlyList<string> Watches => Options.DebuggerOptions.Watches
                .Where(w => !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => w.Name)
                .ToList();

            public Context(ProjectOptions options)
            {
                Options = options;
            }
        }
        

        public SliceVisualizerControl(IToolWindowIntegration integration)
        {
            InitializeComponent();
            DataContext = new Context(integration.ProjectOptions);
        }
    }
}
