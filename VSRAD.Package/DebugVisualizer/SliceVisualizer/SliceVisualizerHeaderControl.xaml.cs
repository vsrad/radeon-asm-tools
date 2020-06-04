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
using VSRAD.Package.ProjectSystem;
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

        public delegate void ToggleHeatMapDelegate(bool heatMapActive);
        private ToggleHeatMapDelegate ToggleHeatMap;

        private Context _context;

        public sealed class Context : DefaultNotifyPropertyChanged
        {
            public ProjectOptions Options { get; }
            public IReadOnlyList<string> Watches => Options.DebuggerOptions.Watches
                .Where(w => !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => w.Name)
                .ToList();

            private int _subgroupSize = 64;
            public int SubgroupSize { get => _subgroupSize; set => SetField(ref _subgroupSize, value); }

            private int _groupsInRow = 1;
            public int GroupsInRow { get => _groupsInRow; set => SetField(ref _groupsInRow, value); }

            private bool _transposedView = false;
            public bool TransposedView { get => _transposedView; set => SetField(ref _transposedView, value); }

            private bool _useHeatMap = false;
            public bool UseHeatMap { get => _useHeatMap; set => SetField(ref _useHeatMap, value); }



            public Context(ProjectOptions options)
            {
                Options = options;
            }
        }

        private void VisualizerOptionsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Context.SubgroupSize):
                    break;
                case nameof(Context.GroupsInRow):
                    break;
                case nameof(Context.TransposedView):
                    break;
                case nameof(Context.UseHeatMap):
                    ToggleHeatMap(_context.UseHeatMap);
                    break;
            }
        }

        public void Setup(IToolWindowIntegration integration, WatchSelectedDelegate watchSelected, ToggleHeatMapDelegate toggleHeatMap)
        {
            InitializeComponent();
            var context = new Context(integration.ProjectOptions);
            context.PropertyChanged += VisualizerOptionsChanged;
            DataContext = context;
            _context = context;
            WatchSelected = watchSelected;
            ToggleHeatMap = toggleHeatMap;
        }

        public string GetSelectedWatch() => WatchSelector.SelectedItem?.ToString();

        private void NewWatchSelected(object sender, SelectionChangedEventArgs e)
        {
            var watchName = (((ComboBox)sender).SelectedItem).ToString();
            WatchSelected(watchName);
        }
    }
}
