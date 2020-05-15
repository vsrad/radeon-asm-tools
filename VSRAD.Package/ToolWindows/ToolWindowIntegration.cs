using System.ComponentModel.Composition;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;

namespace VSRAD.Package.ToolWindows
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

    public sealed class ToolWindowIntegration : IToolWindowIntegration
    {
        public ProjectOptions ProjectOptions { get; }
        public ICommunicationChannel CommunicationChannel { get; }
        public MacroEditManager MacroEditor { get; }

        public event AddWatch AddWatch;

        event DebugBreakEntered IToolWindowIntegration.BreakEntered
        {
            add => _debugger.BreakEntered += value;
            remove => _debugger.BreakEntered -= value;
        }

        private readonly DebuggerIntegration _debugger;

        public ToolWindowIntegration(ProjectOptions options, ICommunicationChannel channel, MacroEditManager macroEditor, DebuggerIntegration debugger)
        {
            ProjectOptions = options;
            CommunicationChannel = channel;
            MacroEditor = macroEditor;
            _debugger = debugger;
        }

        public void AddWatchFromEditor(string watch) => AddWatch(watch);
    }
}
