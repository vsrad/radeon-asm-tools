using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public sealed class SliceVisualizerContext : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action WatchSelected;

        public Options.ProjectOptions Options { get; }

        private string _selectedWatch;
        public string SelectedWatch { get => _selectedWatch; set => SetField(ref _selectedWatch, value); }

        private VariableType _selectedType;
        public VariableType SelectedType { get => _selectedType; set => SetField(ref _selectedType, value); }

        private string _statusString;
        public string StatusString { get => _statusString; set => SetField(ref _statusString, value); }

        public List<string> Watches { get; private set; } = new List<string>();

        private readonly VisualizerContext _visualizerContext;
        private readonly EnvDTE80.WindowVisibilityEvents _windowVisibilityEvents;

        public uint GroupSize => 512; //_visualizerContext.GroupSize; TODO: remove hardcoded 512

        private bool _windowVisible;

        public TypedSliceWatchView SelectedWatchView { get; private set; }

        public SliceVisualizerContext(Options.ProjectOptions options, VisualizerContext visualizerContext, EnvDTE80.WindowVisibilityEvents visibilityEvents)
        {
            Options = options;
            Options.SliceVisualizerOptions.PropertyChanged += SliceOptionChanged;
            
            _visualizerContext = visualizerContext;
            _visualizerContext.GroupFetching += SetupDataFetch;
            _visualizerContext.GroupFetched += DisplayFetchedData;
            _windowVisibilityEvents = visibilityEvents;
            _windowVisibilityEvents.WindowShowing += OnToolWindowVisibilityChanged;
            _windowVisibilityEvents.WindowHiding += OnToolWindowVisibilityChanged;
        }

        public void SetStatusString(int row, int column, string val)
        {
            if (row < 0 || column < 0 || string.IsNullOrEmpty(val))
            {
                StatusString = "";
                return;
            }

            if (SelectedWatchView.IsInactiveCell(row, column))
            {
                StatusString = "Out of bounds";
                return;
            }

            var gNum = SelectedWatchView.GetGroupIndex(row, column);
            var lNum = SelectedWatchView.GetLaneIndex(column);
            StatusString = $"Group# {gNum}, Column# {lNum}, Value: {val}";
        }

        public void NavigateToCell(int sliceRowIndex, int sliceColumnIndex)
        {
            Options.VisualizerOptions.NDRange3D = false; // disable NDRange3D since we dont have implementation of navigation for it

            var groupIndex = SelectedWatchView.GetGroupIndex(sliceRowIndex, sliceColumnIndex);
            var laneIndex = SelectedWatchView.GetLaneIndex(sliceColumnIndex);

            _visualizerContext.GroupIndex.SetGroupIndex(groupIndex);
            _visualizerContext.SelectCell(SelectedWatch, laneIndex);
        }

        private void SliceOptionChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.SliceVisualizerOptions.GroupsInRow):
                case nameof(Options.SliceVisualizerOptions.SubgroupSize):
                case nameof(Options.SliceVisualizerOptions.VisibleColumns):
                    WatchSelectionChanged();
                    break;
            }
        }

        private void SetupDataFetch(object sender, GroupFetchingEventArgs e)
        {
            e.FetchWholeFile |= _windowVisible;
        }

        private void DisplayFetchedData(object sender, GroupFetchedEventArgs e)
        {
            if (!Watches.SequenceEqual(_visualizerContext.BreakData.Watches))
            {
                Watches = new List<string>() { "System" };
                Watches.AddRange(_visualizerContext.BreakData.Watches);
                RaisePropertyChanged(nameof(Watches));
            }
            if (_windowVisible && !string.IsNullOrEmpty(SelectedWatch))
            {
                var watchView = _visualizerContext.BreakData.GetSliceWatch(SelectedWatch, Options.SliceVisualizerOptions.GroupsInRow,
                    (int)_visualizerContext.Options.DebuggerOptions.NGroups);
                var typedView = new TypedSliceWatchView(watchView, SelectedType);
                SelectedWatchView = typedView;
                WatchSelected();
            }
        }

        private void WatchSelectionChanged()
        {
            if (!_windowVisible || string.IsNullOrEmpty(SelectedWatch) || _visualizerContext.BreakData == null)
                return;
            _visualizerContext.GroupIndex.Update();
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            switch (propertyName)
            {
                case nameof(SelectedWatch):
                case nameof(SelectedType):
                    WatchSelectionChanged();
                    break;
            }
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            RaisePropertyChanged(propertyName);

            return true;
        }

        private void OnToolWindowVisibilityChanged(EnvDTE.Window window)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (window.Caption != VSPackage.SliceVisualizerToolWindow.Caption)
                return;
            if (_windowVisible == window.Visible)
                return;
            if (window.Visible)
            {
                _windowVisible = true;
                WatchSelectionChanged();
            }
            else
            {
                _windowVisible = false;
            }
        }
    }
}
