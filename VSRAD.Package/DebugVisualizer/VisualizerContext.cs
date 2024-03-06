using Microsoft.VisualStudio.Shell;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    public sealed class VisualizerNavigationEventArgs : EventArgs
    {
        public uint? GroupIndex { get; set; }
        public uint? WaveIndex { get; set; }
        public BreakpointInfo Breakpoint { get; set; }
    }

    public sealed class VisualizerContext : DefaultNotifyPropertyChanged
    {
        public event EventHandler<GroupFetchingEventArgs> GroupFetching;
        public event EventHandler<GroupFetchedEventArgs> GroupFetched;
        public event EventHandler<VisualizerNavigationEventArgs> NavigationRequested;

        public Options.ProjectOptions Options { get; }
        public GroupIndexSelector GroupIndex { get; }

        public ICommand BreakpointInfoCommand { get; }

        public string _breakpointInfo = "";
        public string BreakpointInfo { get => _breakpointInfo; set => SetField(ref _breakpointInfo, value); }

        private string _status = "No data available";
        public string Status { get => _status; set => SetField(ref _status, value); }

        private bool _watchDataValid;
        public bool WatchDataValid { get => _watchDataValid; set => SetField(ref _watchDataValid, value); }

        private Wavemap.WavemapCell? _wavemapSelection;
        public Wavemap.WavemapCell? WavemapSelection { get => _wavemapSelection; set => SetField(ref _wavemapSelection, value); }

        private bool _groupIndexEditable = true;
        public bool GroupIndexEditable { get => _groupIndexEditable; set => SetField(ref _groupIndexEditable, value); }

        private BreakState _breakState;
        public BreakState BreakState { get => _breakState; private set => SetField(ref _breakState, value); }

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

            BreakpointInfoCommand = new WpfDelegateCommand(BreakpointInfoClick);
        }

        public BreakpointInfo GetBreakpointByThreadId(uint threadId)
        {
            var waveStatus = BreakState.GetWaveStatus(threadId / BreakState.Dispatch.WaveSize);
            return waveStatus.BreakpointIndex is uint idx ? BreakState.Target.Breakpoints[(int)idx] : null;
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
                WatchDataValid = false;
                Status = FormatErrorStatusString(error, LastRunTime);
            }
            WavemapSelection = null;
            BreakpointInfo = FormatBreakpointInfoString(BreakState);
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
            Status = FormatBreakStatusString(BreakState, LastRunTime);
        }

        private void BreakpointInfoClick(object param)
        {
            if (BreakState != null && param is FrameworkContentElement infoLink && infoLink.Parent is UIElement infoBlock)
            {
                var validBreakpoints = BreakState.BreakpointIndexPerInstance.Values
                    .Select(i => (Index: i, IsHit: BreakState.HitBreakpoints.Contains(i))).OrderBy(i => i.IsHit ? 0 : 1).Distinct().ToList();

                if (BreakState.HitBreakpoints.Count == 1 && validBreakpoints.Count == 1)
                {
                    var breakpoint = BreakState.Target.Breakpoints[(int)BreakState.HitBreakpoints.First()];
                    NavigationRequested?.Invoke(this, new VisualizerNavigationEventArgs { Breakpoint = breakpoint });
                }
                else
                {
                    var menu = new ContextMenu { PlacementTarget = infoBlock };

                    foreach (var (index, isHit) in validBreakpoints)
                    {
                        var breakpoint = BreakState.Target.Breakpoints[(int)index];
                        var item = new MenuItem { Header = new TextBlock { Text = breakpoint.Location } }; // use TextBlock because Location may contain underscores
                        item.IsChecked = isHit;
                        item.Click += (s, _) => NavigationRequested?.Invoke(this, new VisualizerNavigationEventArgs { Breakpoint = breakpoint });
                        menu.Items.Add(item);
                    }
                    menu.IsOpen = true;
                }
            }
        }

        private static string FormatBreakpointInfoString(BreakState breakState)
        {
            if (breakState != null)
            {
                var numBreakpointsHit = breakState.HitBreakpoints.Count;
                var numBreakpointsValid = breakState.BreakpointIndexPerInstance.Values.Distinct().Count();
                if (numBreakpointsHit == 1 && numBreakpointsValid == 1)
                {
                    var breakpoint = breakState.Target.Breakpoints[(int)breakState.HitBreakpoints.First()];
                    return breakpoint.Location;
                }
                else
                {
                    return string.Format(CultureInfo.InvariantCulture, "Breakpoints hit: {0}/{1}", numBreakpointsHit, numBreakpointsValid);
                }
            }
            else
            {
                return "";
            }
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
            if (!string.IsNullOrEmpty(breakState.Dispatch.StatusString))
                status.Append(" | Status: ").Append(breakState.Dispatch.StatusString);
            status.AppendFormat(CultureInfo.InvariantCulture, " | Last run: {0:HH:mm:ss}", lastRunAt);
            status.Append(" |");
            return status.ToString();
        }

        private static string FormatErrorStatusString(Error error, DateTime lastRunAt)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} | Last run: {1:HH:mm:ss}", error.Message, lastRunAt);
        }
    }
}
