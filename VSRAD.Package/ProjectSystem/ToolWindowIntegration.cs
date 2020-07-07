using Microsoft.VisualStudio.ProjectSystem;
using System.ComponentModel.Composition;
using VSRAD.Package.Options;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void AddWatch(string watch);

    public interface IToolWindowIntegration
    {
        ProjectOptions ProjectOptions { get; }
        IProject Project { get; }
        ICommunicationChannel CommunicationChannel { get; }

        event AddWatch AddWatch;

        event DebugBreakEntered BreakEntered;

        void AddWatchFromEditor(string watch);
    }

    [Export(typeof(IToolWindowIntegration))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ToolWindowIntegration : IToolWindowIntegration
    {
        public IProject Project { get; }
        public ICommunicationChannel CommunicationChannel { get; }

        public ProjectOptions ProjectOptions => Project.Options;

        public event AddWatch AddWatch;

        event DebugBreakEntered IToolWindowIntegration.BreakEntered
        {
            add => _debugger.BreakEntered += value;
            remove => _debugger.BreakEntered -= value;
        }

        private readonly DebuggerIntegration _debugger;

        [ImportingConstructor]
        public ToolWindowIntegration(IProject project, ICommunicationChannel channel, DebuggerIntegration debugger)
        {
            Project = project;
            CommunicationChannel = channel;
            _debugger = debugger;
        }

        public void AddWatchFromEditor(string watch) => AddWatch(watch);
    }
}
