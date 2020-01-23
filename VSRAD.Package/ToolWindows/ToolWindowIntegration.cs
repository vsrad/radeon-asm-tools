using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ToolWindows
{
    public delegate void AddWatch(string watch);

    public interface IToolWindowIntegration
    {
        ProjectOptions ProjectOptions { get; }

        event AddWatch AddWatch;
        event DebugBreakEntered BreakEntered;

        void RerunDebug();
        T GetExport<T>();
        void AddWatchFromEditor(string watch);
    }

    public sealed class ToolWindowIntegration : IToolWindowIntegration
    {
        public event AddWatch AddWatch;

        event DebugBreakEntered IToolWindowIntegration.BreakEntered
        {
            add => _debugger.BreakEntered += value;
            remove => _debugger.BreakEntered -= value;
        }

        public ProjectOptions ProjectOptions => _project.Options;

        public bool DebugInProgress => _debugger.DebugInProgress;

        private readonly ConfiguredProject _configuredProject;
        private readonly IProject _project;
        private readonly DebuggerIntegration _debugger;

        [ImportingConstructor]
        internal ToolWindowIntegration(ConfiguredProject configuredProject, IProject project, DebuggerIntegration debugger)
        {
            _configuredProject = configuredProject;
            _project = project;
            _debugger = debugger;
        }

        public void AddWatchFromEditor(string watch) => AddWatch(watch);

        void IToolWindowIntegration.RerunDebug()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (DebugInProgress) _debugger.Rerun();
        }

        public T GetExport<T>() => _configuredProject.GetExport<T>();
    }
}
