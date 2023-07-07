using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.DebugVisualizer.SliceVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void AddWatch(string watch);

    public interface IToolWindowIntegration
    {
        ProjectOptions ProjectOptions { get; }
        IProject Project { get; }
        ICommunicationChannel CommunicationChannel { get; }

        event AddWatch AddWatch;

        VisualizerContext GetVisualizerContext();
        SliceVisualizerContext GetSliceVisualizerContext();

        void AddWatchFromEditor(string watch);
        void OpenFileInEditor(string file, uint line);
    }

    [Export(typeof(IToolWindowIntegration))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ToolWindowIntegration : IToolWindowIntegration
    {
        public IProject Project { get; }
        public ProjectOptions ProjectOptions => Project.Options;
        public ICommunicationChannel CommunicationChannel { get; }

        public event AddWatch AddWatch;

        private readonly IDebuggerIntegration _debugger;
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public ToolWindowIntegration(IProject project, ICommunicationChannel channel, IDebuggerIntegration debugger, SVsServiceProvider serviceProvider)
        {
            Project = project;
            CommunicationChannel = channel;
            _debugger = debugger;
            _serviceProvider = serviceProvider;
        }

        private VisualizerContext _visualizerContext;
        public VisualizerContext GetVisualizerContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_visualizerContext == null)
                _visualizerContext = new VisualizerContext(ProjectOptions, CommunicationChannel, _debugger);
            return _visualizerContext;
        }

        private SliceVisualizerContext _sliceVisualizerContext;
        public SliceVisualizerContext GetSliceVisualizerContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_sliceVisualizerContext == null)
            {
                var dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
                Assumes.Present(dte);
                var dteEvents = (EnvDTE80.Events2)dte.Events;

                _sliceVisualizerContext = new SliceVisualizerContext(ProjectOptions, GetVisualizerContext(), dteEvents.WindowVisibilityEvents);
            }
            return _sliceVisualizerContext;
        }

        public void AddWatchFromEditor(string watch) => AddWatch(watch);

        public void OpenFileInEditor(string file, uint line)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsEditor.OpenFileInEditor(_serviceProvider, file, line, null, forceOppositeTab: false, preserveActiveDoc: false);
        }
    }
}
