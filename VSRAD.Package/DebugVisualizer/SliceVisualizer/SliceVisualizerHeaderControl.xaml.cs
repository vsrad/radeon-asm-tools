using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VSRAD.Package.Options;
using VSRAD.Package.ToolWindows;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    /// <summary>
    /// Interaction logic for SliceVisualizerHeaderControl.xaml
    /// </summary>
    public partial class SliceVisualizerHeaderControl : UserControl
    {
        public delegate void WatchSelectedDelegate(string watchName);
        private WatchSelectedDelegate WatchSelected;

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

        public void Setup(IToolWindowIntegration integration, WatchSelectedDelegate watchSelected)
        {
            InitializeComponent();
            DataContext = new Context(integration.ProjectOptions);
            WatchSelected = watchSelected;
        }

        private void NewWatchSelected(object sender, SelectionChangedEventArgs e)
        {
            var watchName = (((ComboBox)sender).SelectedItem).ToString();
            WatchSelected(watchName);
        }
    }
}
