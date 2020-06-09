using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.DebugVisualizer.SliceVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void AddWatch(string watch);

    public interface IToolWindowIntegration
    {
        ProjectOptions ProjectOptions { get; }
        ICommunicationChannel CommunicationChannel { get; }
        MacroEditManager MacroEditor { get; }

        event AddWatch AddWatch;

        VisualizerContext GetVisualizerContext();

        void AddWatchFromEditor(string watch);
    }

    [Export(typeof(IToolWindowIntegration))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ToolWindowIntegration : IToolWindowIntegration
    {
        public ProjectOptions ProjectOptions => _project.Options;
        public ICommunicationChannel CommunicationChannel { get; }
        public MacroEditManager MacroEditor { get; }

        public event AddWatch AddWatch;

        private readonly IProject _project;
        private readonly DebuggerIntegration _debugger;
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public ToolWindowIntegration(IProject project, DebuggerIntegration debugger, SVsServiceProvider serviceProvider, ICommunicationChannel channel, MacroEditManager macroEditor)
        {
            _project = project;
            _debugger = debugger;
            _serviceProvider = serviceProvider;
            CommunicationChannel = channel;
            MacroEditor = macroEditor;
        }

        private VisualizerContext _visualizerContext;
        public VisualizerContext GetVisualizerContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_visualizerContext == null)
            {
                var dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
                Assumes.Present(dte);
                var dteEvents = (EnvDTE80.Events2)dte.Events;

                var sliceVisibility = new SliceVisualizerContext(dteEvents.WindowVisibilityEvents);
                _visualizerContext = new VisualizerContext(ProjectOptions, CommunicationChannel, _debugger, sliceVisibility);
            }
            return _visualizerContext;
        }

        public void AddWatchFromEditor(string watch) => AddWatch(watch);
    }
}
