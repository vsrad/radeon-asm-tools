using System;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class GroupFetchingEventArgs : EventArgs
    {
        public bool FetchWholeFile { get; set; }
    }

    public sealed class GroupFetchedEventArgs : EventArgs
    {
        public BreakStateDispatchParameters DispatchParameters { get; }
        public string Warning { get; }

        public GroupFetchedEventArgs(BreakStateDispatchParameters dispatchParameters, string warning)
        {
            DispatchParameters = dispatchParameters;
            Warning = warning;
        }
    }

    public sealed class VisualizerContext : DefaultNotifyPropertyChanged
    {
        public event EventHandler<GroupFetchingEventArgs> GroupFetching;
        public event EventHandler<GroupFetchedEventArgs> GroupFetched;

        public Options.ProjectOptions Options { get; }
        public GroupIndexSelector GroupIndex { get; }

        private string _status = "No data available";
        public string Status { get => _status; set => SetField(ref _status, value); }

        private bool _watchDataValid = true;
        public bool WatchDataValid { get => _watchDataValid; set => SetField(ref _watchDataValid, value); }

        private int _canvasWidth = 100;
        public int CanvasWidth { get => _canvasWidth; set => SetField(ref _canvasWidth, value); }

        private int _canvasHeight = 10;
        public int CanvasHeight { get => _canvasHeight; set => SetField(ref _canvasHeight, value); }

        private Wavemap.WaveInfo _currentWaveInfo;
        public Wavemap.WaveInfo CurrentWaveInfo { get => _currentWaveInfo; set => SetField(ref _currentWaveInfo, value); }

        private bool _groupIndexEditable = true;
        public bool GroupIndexEditable { get => _groupIndexEditable; set => SetField(ref _groupIndexEditable, value); }

        public BreakStateData BreakData => _breakState?.Data;

        private readonly ICommunicationChannel _channel;
        private BreakState _breakState;

        public VisualizerContext(Options.ProjectOptions options, ICommunicationChannel channel, DebuggerIntegration debugger)
        {
            Options = options;
            Options.DebuggerOptions.PropertyChanged += OptionsChanged;
            _channel = channel;

            debugger.BreakEntered += EnterBreak;

            GroupIndex = new GroupIndexSelector(options);
            GroupIndex.IndexChanged += GroupIndexChanged;
        }

        private void OptionsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.DebuggerOptions.Counter):
                    WatchDataValid = false;
                    break;
            }
        }

        private void EnterBreak(object sender, BreakState breakState)
        {
            _breakState = breakState;
            WatchDataValid = breakState != null;
            if (WatchDataValid)
                GroupIndex.UpdateOnBreak(breakState);
        }

        private void GroupIndexChanged(object sender, GroupIndexChangedEventArgs e)
        {
            if (_breakState == null)
                return;

            e.DataGroupCount = (uint)_breakState.Data.GetGroupCount((int)e.GroupSize, (int)Options.DebuggerOptions.WaveSize, (int)Options.DebuggerOptions.NGroups);
            WatchDataValid = e.IsValid = e.GroupIndex < e.DataGroupCount;
            if (!WatchDataValid)
                return;

            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() => ChangeGroupAsync(e));
        }

        private async Task ChangeGroupAsync(GroupIndexChangedEventArgs e)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var fetchArgs = new GroupFetchingEventArgs();
            GroupFetching(this, fetchArgs);

            Status = fetchArgs.FetchWholeFile ? "Fetching results" : $"Fetching group {e.Coordinates}";
            GroupIndexEditable = false;

            var warning = await _breakState.Data.ChangeGroupWithWarningsAsync(_channel, (int)e.GroupIndex, (int)e.GroupSize,
                (int)Options.DebuggerOptions.WaveSize, (int)Options.DebuggerOptions.NGroups, fetchArgs.FetchWholeFile);

            GroupFetched(this, new GroupFetchedEventArgs(_breakState.DispatchParameters, warning));

            var status = new StringBuilder();
            status.AppendFormat("{0} groups, wave size: {1}, last run at: {2}",
                e.DataGroupCount, Options.DebuggerOptions.WaveSize, _breakState.ExecutedAt.ToString("HH:mm:ss"));
            if (_breakState.DispatchParameters?.StatusString is string statusStr && statusStr.Length != 0)
            {
                status.Append(", status: ");
                status.Append(statusStr);
            }
            Status = status.ToString();
            GroupIndexEditable = true;
        }
    }
}
