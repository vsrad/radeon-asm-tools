using System;
using System.Windows.Controls;
using VSRAD.Package.ToolWindows;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class VisualizerHeaderControl : UserControl
    {
        public event Action GroupSizeChanged;

        public sealed class Context : DefaultNotifyPropertyChanged
        {
            public Options.ProjectOptions Options { get; }

            private bool _showColumnsField = true;
            public bool ShowColumnsField { get => _showColumnsField; set => SetField(ref _showColumnsField, value); }

            private bool _showAppArgsField = true;
            public bool ShowAppArgsField { get => _showAppArgsField; set => SetField(ref _showAppArgsField, value); }

            private bool _showBreakArgsField = true;
            public bool ShowBreakArgsField { get => _showBreakArgsField; set => SetField(ref _showBreakArgsField, value); }

            private bool _groupIndexEditable = true;
            public bool GroupIndexEditable { get => _groupIndexEditable; set => SetField(ref _groupIndexEditable, value); }

            private string _status = "No data available";
            public string Status { get => _status; set => SetField(ref _status, value); }

            public GroupIndexSelector GroupIndex { get; }

            public Context(Options.ProjectOptions options, CalculateGroupCount getGroupCount, GroupSelectionChange groupSelectionChanged)
            {
                Options = options;
                GroupIndex = new GroupIndexSelector(options.VisualizerOptions, getGroupCount, groupSelectionChanged);
            }
        }

        public uint GroupSize => Data.GroupIndex.GroupSize;

        private Context Data => (Context)DataContext;

        private DateTime _lastRunAt;

        public VisualizerHeaderControl() => InitializeComponent();

        public void Setup(IToolWindowIntegration integration, CalculateGroupCount getGroupCount, GroupSelectionChange groupSelectionChanged)
        {
            DataContext = new Context(integration.ProjectOptions, getGroupCount, groupSelectionChanged);
            Data.GroupIndex.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(GroupIndexSelector.GroupSize):
                        GroupSizeChanged();
                        break;
                }
            };
        }

        public void OnDataAvailable()
        {
            _lastRunAt = DateTime.Now;
            Data.GroupIndex.OnDataAvailable();
        }

        public void OnPendingDataRequest(string coordinates)
        {
            Data.Status = $"Fetching group {coordinates}";
            Data.GroupIndexEditable = false;
        }

        public void OnDataRequestCompleted(uint groupCount)
        {
            Data.Status = $"{groupCount} groups, last run at {_lastRunAt.ToString("HH:mm:ss")}";
            Data.GroupIndexEditable = true;
        }
    }
}
