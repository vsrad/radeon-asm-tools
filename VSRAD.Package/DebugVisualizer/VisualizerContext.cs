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
        public DateTime LastRunTime { get; private set; }

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

        public BreakpointInfo GetBreakpointByThreadId(uint threadId) =>
            TryGetWaveHitInfo(BreakState.GroupIndex, threadId / BreakState.Dispatch.WaveSize, out _, out var breakpoint, out _) ? breakpoint : null;

        /// <returns>True if the wave exist, false otherwise. Breakpoint may be null even if the wave exist, in which case breakpointIdx and execMask should be treated as invalid.</returns>
        public bool TryGetWaveHitInfo(uint groupIndex, uint waveIndex, out uint breakpointIdx, out BreakpointInfo breakpoint, out ulong execMask)
        {
            (breakpointIdx, breakpoint) = (0, null);
            if (!BreakState.TryGetGlobalSystemData(groupIndex, waveIndex, out uint magicNumber, out var instanceId, out execMask))
                return false;
            if ((magicNumber == Options.VisualizerOptions.MagicNumber || !Options.VisualizerOptions.CheckMagicNumber)
                    && BreakState.BreakpointIndexPerInstance.TryGetValue(instanceId, out breakpointIdx)
                    && breakpointIdx < BreakState.Target.Breakpoints.Count)
                breakpoint = BreakState.Target.Breakpoints[(int)breakpointIdx];
            return true;
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

        private void EnterBreak(object sender, Result<BreakState> breakResult)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            LastRunTime = DateTime.Now;
            if (breakResult.TryGetResult(out var breakState, out var error))
            {
                BreakState = breakState;
                GroupIndex.UpdateOnBreak(breakState); // Will invoke GroupIndexChanged, see below
            }
            else
            {
                BreakState = null;
                Wavemap = null;
                WatchDataValid = false;
                Status = FormatErrorStatusString(error, LastRunTime);
            }
            WavemapSelection = null;
            VSPackage.VisualizerToolWindow?.BringToFront();
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

                var warning = await BreakState.ChangeGroupWithWarningsAsync(_channel, e.GroupIndex, fetchArgs.FetchWholeFile);

                GroupFetched(this, new GroupFetchedEventArgs(BreakState.Dispatch, warning));
                GroupIndexEditable = true;
            }
            WatchDataValid = e.IsGroupIndexValid;
            Wavemap = new Wavemap.WavemapView(TryGetWaveHitInfo);
            Status = FormatBreakStatusString(BreakState, LastRunTime);
        }

        private static string FormatBreakStatusString(BreakState breakState, DateTime lastRunAt)
        {
            var status = new StringBuilder();
            status.AppendFormat(CultureInfo.InvariantCulture, "Groups: {0}", breakState.NumGroups);
            if (breakState.Dispatch.NDRange3D)
                status.AppendFormat(CultureInfo.InvariantCulture, " | Group size: ({0}, {1}, {2})", breakState.Dispatch.GroupSizeX, breakState.Dispatch.GroupSizeY, breakState.Dispatch.GroupSizeZ);
            else
                status.AppendFormat(CultureInfo.InvariantCulture, " | Group size: {0}", breakState.Dispatch.GroupSizeX);
            status.AppendFormat(CultureInfo.InvariantCulture, " | Wave size: {0}", breakState.Dispatch.WaveSize);
            status.AppendFormat(CultureInfo.InvariantCulture, " | Breakpoints hit: {0}", breakState.HitBreakpoints.Count);
            if (!string.IsNullOrEmpty(breakState.Dispatch.StatusString))
                status.Append(" | Status: ").Append(breakState.Dispatch.StatusString);
            status.AppendFormat(CultureInfo.InvariantCulture, " | Last run: {0:HH:mm:ss}", lastRunAt);
            return status.ToString();
        }

        private static string FormatErrorStatusString(Error error, DateTime lastRunAt)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} | Last run: {1:HH:mm:ss}", error.Message, lastRunAt);
        }
    }
}
