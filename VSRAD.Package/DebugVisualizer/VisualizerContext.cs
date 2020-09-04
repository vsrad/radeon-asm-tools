using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public class GroupFetchingEventArgs : EventArgs
    {
        public bool FetchWholeFile { get; set; }
    }

    public class GroupFetchedEventArgs : EventArgs
    {
        public string Warning { get; }

        public GroupFetchedEventArgs(string warning)
        {
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

        private bool _watchesValid = true;
        public bool WatchesValid { get => _watchesValid; set => SetField(ref _watchesValid, value); }

        private bool _groupIndexEditable = true;
        public bool GroupIndexEditable { get => _groupIndexEditable; set => SetField(ref _groupIndexEditable, value); }

        public BreakStateData BreakData => _breakState?.Data;

        private readonly ICommunicationChannel _channel;
        private BreakState _breakState;

        public VisualizerContext(Options.ProjectOptions options, ICommunicationChannel channel, DebuggerIntegration debugger)
        {
            Options = options;
            _channel = channel;

            debugger.BreakEntered += EnterBreak;

            GroupIndex = new GroupIndexSelector(options);
            GroupIndex.IndexChanged += GroupIndexChanged;
        }

        private void EnterBreak(BreakState breakState)
        {
            _breakState = breakState;
            UpdateProjectState(breakState);
            WatchesValid = breakState != null;
            if (WatchesValid)
                GroupIndex.Update();
        }

        private void UpdateProjectState(BreakState breakState)
        {
            if (breakState == null) return;

            Options.VisualizerOptions.NDRange3D = breakState.GridY != 0 && breakState.GridZ != 0;
            Options.DebuggerOptions.GroupSize = breakState.GroupX;
            GroupIndex.DimX = breakState.GridX;
            GroupIndex.DimY = breakState.GridY;
            GroupIndex.DimZ = breakState.GridZ;
            // TODO: handle 3d group sizes and wavesize
        }

        private void GroupIndexChanged(object sender, GroupIndexChangedEventArgs e)
        {
            if (_breakState == null)
                return;

            e.DataGroupCount = (uint)_breakState.Data.GetGroupCount((int)e.GroupSize, (int)Options.DebuggerOptions.NGroups);
            WatchesValid = e.IsValid = e.GroupIndex < e.DataGroupCount;
            if (!WatchesValid)
                return;

            VSPackage.TaskFactory.RunAsync(() => ChangeGroupAsync(e));
        }

        private async Task ChangeGroupAsync(GroupIndexChangedEventArgs e)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var fetchArgs = new GroupFetchingEventArgs();
            GroupFetching(this, fetchArgs);

            Status = fetchArgs.FetchWholeFile ? "Fetching results" : $"Fetching group {e.Coordinates}";
            GroupIndexEditable = false;

            var warning = await _breakState.Data.ChangeGroupWithWarningsAsync(_channel, (int)e.GroupIndex, (int)e.GroupSize,
                (int)Options.DebuggerOptions.NGroups, fetchArgs.FetchWholeFile);

            GroupFetched(this, new GroupFetchedEventArgs(warning));

            var status = new StringBuilder();
            status.AppendFormat("{0} groups, last run at {1}, total: {2}ms, execute: {3}ms",
                e.DataGroupCount, _breakState.ExecutedAt.ToString("HH:mm:ss"), _breakState.TotalElapsedMilliseconds, _breakState.ExecElapsedMilliseconds);
            if (!string.IsNullOrEmpty(_breakState.StatusString))
            {
                status.Append(", status:");
                status.Append(_breakState.StatusString);
            }
            Status = status.ToString();
            GroupIndexEditable = true;
        }
    }
}
