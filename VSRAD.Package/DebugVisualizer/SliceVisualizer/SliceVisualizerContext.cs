using Microsoft.VisualStudio.Shell;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public sealed class SliceVisualizerContext : DefaultNotifyPropertyChanged
    {
        private bool _windowVisible;
        public bool WindowVisibile { get => _windowVisible; set => SetField(ref _windowVisible, value); }

        private readonly EnvDTE80.WindowVisibilityEvents _windowVisibilityEvents;

        public SliceVisualizerContext(EnvDTE80.WindowVisibilityEvents visibilityEvents)
        {
            _windowVisibilityEvents = visibilityEvents;
            _windowVisibilityEvents.WindowShowing += OnToolWindowVisibilityChanged;
            _windowVisibilityEvents.WindowHiding += OnToolWindowVisibilityChanged;
        }

        private void OnToolWindowVisibilityChanged(EnvDTE.Window window)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (window.Caption == VSPackage.SliceVisualizerToolWindow.Caption)
                WindowVisibile = window.Visible;
        }
    }
}
