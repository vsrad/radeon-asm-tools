using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public sealed class SliceVisualizerContext : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<TypedSliceWatchView> WatchSelected;

        private int _subgroupSize = 64;
        public int SubgroupSize { get => _subgroupSize; set => SetField(ref _subgroupSize, value); }

        private int _groupsInRow = 1;
        public int GroupsInRow { get => _groupsInRow; set => SetField(ref _groupsInRow, value); }

        private string _selectedWatch;
        public string SelectedWatch { get => _selectedWatch; set => SetField(ref _selectedWatch, value); }

        private VariableType _selectedType;
        public VariableType SelectedType { get => _selectedType; set => SetField(ref _selectedType, value); }

        public List<string> Watches { get; } = new List<string>();

        private readonly VisualizerContext _visualizerContext;
        private readonly EnvDTE80.WindowVisibilityEvents _windowVisibilityEvents;

        private bool _windowVisible;

        public SliceVisualizerContext(VisualizerContext visualizerContext, EnvDTE80.WindowVisibilityEvents visibilityEvents)
        {
            _visualizerContext = visualizerContext;
            _visualizerContext.GroupFetching += SetupDataFetch;
            _visualizerContext.GroupFetched += DisplayFetchedData;
            _windowVisibilityEvents = visibilityEvents;
            _windowVisibilityEvents.WindowShowing += OnToolWindowVisibilityChanged;
            _windowVisibilityEvents.WindowHiding += OnToolWindowVisibilityChanged;
        }

        private void SetupDataFetch(object sender, GroupFetchingEventArgs e)
        {
            e.FetchWholeFile = _windowVisible;
        }

        private void DisplayFetchedData(object sender, GroupFetchedEventArgs e)
        {
            if (!Watches.SequenceEqual(_visualizerContext.BreakData.Watches))
            {
                Watches.Clear();
                Watches.Add("System");
                Watches.AddRange(_visualizerContext.BreakData.Watches);
                RaisePropertyChanged(nameof(Watches));
            }
            if (_windowVisible && !string.IsNullOrEmpty(SelectedWatch))
            {
                var watchView = _visualizerContext.BreakData.GetSliceWatch(SelectedWatch, GroupsInRow);
                var typedView = new TypedSliceWatchView(watchView, SelectedType);
                WatchSelected(this, typedView);
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
                case nameof(SubgroupSize):
                case nameof(GroupsInRow):
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
