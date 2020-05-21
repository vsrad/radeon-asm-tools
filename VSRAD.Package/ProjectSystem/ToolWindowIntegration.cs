using Microsoft.VisualStudio.ProjectSystem;
using System.ComponentModel.Composition;
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

        event DebugBreakEntered BreakEntered;

        void AddWatchFromEditor(string watch);
    }

    [Export(typeof(IToolWindowIntegration))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ToolWindowIntegration : IToolWindowIntegration
    {
        public ICommunicationChannel CommunicationChannel { get; }
        public MacroEditManager MacroEditor { get; }

        public ProjectOptions ProjectOptions => _project.Options;

        public event AddWatch AddWatch;

        event DebugBreakEntered IToolWindowIntegration.BreakEntered
        {
            add => _debugger.BreakEntered += value;
            remove => _debugger.BreakEntered -= value;
        }

        private readonly IProject _project;
        private readonly DebuggerIntegration _debugger;

        [ImportingConstructor]
        public ToolWindowIntegration(IProject project, DebuggerIntegration debugger, ICommunicationChannel channel, MacroEditManager macroEditor)
        {
            _project = project;
            _debugger = debugger;
            CommunicationChannel = channel;
            MacroEditor = macroEditor;
        }

        public void AddWatchFromEditor(string watch) => AddWatch(watch);
    }
}
