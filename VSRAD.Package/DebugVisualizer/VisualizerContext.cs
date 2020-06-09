using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
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
        public event EventHandler<GroupFetchedEventArgs> GroupFetched;
        public Options.ProjectOptions Options { get; }
        public GroupIndexSelector GroupIndex { get; }

        private string _status = "No data available";
        public string Status { get => _status; set => SetField(ref _status, value); }

        private bool _watchesValid = true;
        public bool WatchesValid { get => _watchesValid; set => SetField(ref _watchesValid, value); }

        private bool _groupIndexEditable = true;
        public bool GroupIndexEditable { get => _groupIndexEditable; set => SetField(ref _groupIndexEditable, value); }

        public uint GroupSize => GroupIndex.GroupSize;
        public BreakStateData BreakData => _breakState?.Data;

        private readonly ICommunicationChannel _channel;
        private BreakState _breakState;

        public VisualizerContext(Options.ProjectOptions options, ICommunicationChannel channel)
        {
            Options = options;
            _channel = channel;

            GroupIndex = new GroupIndexSelector(options.VisualizerOptions);
            GroupIndex.IndexChanged += GroupIndexChanged;
            GroupIndex.PropertyChanged += OptionsChanged;
            Options.DebuggerOptions.PropertyChanged += OptionsChanged;
        }

        public void EnterBreak(BreakState breakState)
        {
            _breakState = breakState;
            WatchesValid = breakState != null;
            if (WatchesValid)
                GroupIndex.Update();
        }

        private void OptionsChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(GroupIndex.GroupSize):
                    RaisePropertyChanged(nameof(GroupSize));
                    break;
                case nameof(Options.DebuggerOptions.NGroups):
                    GroupIndex.Update();
                    break;
            }
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
            Status = $"Fetching group {e.Coordinates}";
            GroupIndexEditable = false;

            var warning = await _breakState.Data.ChangeGroupWithWarningsAsync(_channel, (int)e.GroupIndex, (int)e.GroupSize, (int)Options.DebuggerOptions.NGroups);
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
