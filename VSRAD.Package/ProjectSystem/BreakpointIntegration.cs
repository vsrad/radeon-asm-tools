using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using VSRAD.Package.Options;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    [Export]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class BreakpointIntegration
    {
        private static readonly TimeSpan _pollDelay = new TimeSpan(250 * TimeSpan.TicksPerMillisecond);
        private readonly SVsServiceProvider _serviceProvider;
        private readonly IProject _project;

        private readonly CancellationTokenSource _pollCts = new CancellationTokenSource();
        private readonly Dictionary<string, EnvDTE.Breakpoint> _activeBreakpoints = new Dictionary<string, EnvDTE.Breakpoint>();

        private EnvDTE.Debugger _debuggerDte;

        [ImportingConstructor]
        public BreakpointIntegration(SVsServiceProvider serviceProvider, IProject project)
        {
            _serviceProvider = serviceProvider;
            _project = project;
            _project.Loaded += Initialize;
            _project.Unloaded += _pollCts.Cancel;
        }

        public void Initialize(ProjectOptions options)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(SDTE)) as EnvDTE.DTE;
            Assumes.Present(dte);
            _debuggerDte = dte.Debugger;
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(PollBreakpointsAsync);
        }

        private async Task PollBreakpointsAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            while (!_pollCts.IsCancellationRequested)
            {
                if (_debuggerDte.Breakpoints != null)
                {
                    foreach (EnvDTE.Breakpoint newBp in _debuggerDte.Breakpoints)
                    {
                        if (!newBp.Enabled)
                            continue;
                        // If the breakpoint is enabled and does not match the previously active one, disable the latter
                        if (_activeBreakpoints.TryGetValue(newBp.File, out var oldBp))
                        {
                            if (newBp != oldBp)
                            {
                                oldBp.Enabled = false;
                                _activeBreakpoints[newBp.File] = newBp;
                            }
                        }
                        else
                        {
                            _activeBreakpoints[newBp.File] = newBp;
                        }
                    }
                }

                await Task.Delay(_pollDelay);
            }
        }
    }
}
