using Microsoft.VisualStudio.Shell;
using System;
using System.Globalization;
using System.Text;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

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

        private bool _watchDataValid;
        public bool WatchDataValid { get => _watchDataValid; set => SetField(ref _watchDataValid, value); }

        private Wavemap.WavemapView _wavemap;
        public Wavemap.WavemapView Wavemap { get => _wavemap; set => SetField(ref _wavemap, value); }

        private Wavemap.WaveInfo _wavemapSelection;
        public Wavemap.WaveInfo WavemapSelection { get => _wavemapSelection; set => SetField(ref _wavemapSelection, value); }

        private bool _groupIndexEditable = true;
        public bool GroupIndexEditable { get => _groupIndexEditable; set => SetField(ref _groupIndexEditable, value); }

        public BreakState BreakState { get; private set; }
        public BreakStateData BreakData => BreakState?.Data;

        private readonly ICommunicationChannel _channel;

        public VisualizerContext(Options.ProjectOptions options, ICommunicationChannel channel, IDebuggerIntegration debugger)
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
            ThreadHelper.ThrowIfNotOnUIThread();

            BreakState = breakState;
            if (breakState != null)
            {
                GroupIndex.UpdateOnBreak((uint)breakState.Data.NumThreadsInProgram, breakState.DispatchParameters); // Will invoke GroupIndexChanged, see below
                VSPackage.VisualizerToolWindow?.BringToFront();
            }
            else
            {
                Status = "Run failed, see the Output window for more details";
                Wavemap = null;
            }
            WavemapSelection = null;
        }

        private void GroupIndexChanged(object sender, GroupIndexChangedEventArgs e)
        {
            if (BreakState != null)
                ThreadHelper.JoinableTaskFactory.RunAsyncWithErrorHandling(() => ChangeGroupAsync(e));
        }

        private async Task ChangeGroupAsync(GroupIndexChangedEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (e.IsGroupIndexValid)
            {
                var fetchArgs = new GroupFetchingEventArgs();
                GroupFetching(this, fetchArgs);

                Status = fetchArgs.FetchWholeFile ? "Fetching results" : $"Fetching group {e.Coordinates}";
                GroupIndexEditable = false;

                var warning = await BreakState.Data.ChangeGroupWithWarningsAsync(_channel, (int)e.GroupIndex, (int)e.GroupSize,
                    (int)Options.DebuggerOptions.WaveSize, fetchArgs.FetchWholeFile);

                GroupFetched(this, new GroupFetchedEventArgs(BreakState.DispatchParameters, warning));
                GroupIndexEditable = true;
            }
            WatchDataValid = e.IsGroupIndexValid;
            Wavemap = new Wavemap.WavemapView((uint groupIndex, uint waveIndex, out uint[] systemData) =>
                BreakData.TryGetGlobalSystemData((int)groupIndex, (int)waveIndex, (int)Options.DebuggerOptions.GroupSize, (int)Options.DebuggerOptions.WaveSize, out systemData));
            Status = FormatBreakStatusString(BreakState, Options.DebuggerOptions);
        }

        private static string FormatBreakStatusString(BreakState breakState, Options.DebuggerOptions debuggerOptions)
        {
            var groupCount = breakState.Data.NumThreadsInProgram / MathUtils.RoundUpToMultiple(debuggerOptions.GroupSize, debuggerOptions.WaveSize);
            var waveSize = debuggerOptions.WaveSize;

            var status = new StringBuilder();
            status.AppendFormat(CultureInfo.InvariantCulture, "Groups: {0}, wave size: {1}, last run at: {2:HH:mm:ss}", groupCount, waveSize, breakState.ExecutedAt);
            if (breakState.DispatchParameters?.StatusString is string dispatchStatus && dispatchStatus.Length != 0)
                status.Append(", status: ").Append(dispatchStatus);
            return status.ToString();
        }
    }
}
