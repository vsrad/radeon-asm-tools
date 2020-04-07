using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
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

        private CancellationTokenSource _pollCts;

        [ImportingConstructor]
        public BreakpointIntegration(SVsServiceProvider serviceProvider, IProject project)
        {
            _serviceProvider = serviceProvider;
            _project = project;
            _project.Loaded += Initialize;
            _project.Unloaded += () => _pollCts?.Cancel();
        }

        public void Initialize(ProjectOptions options)
        {
            SetSingleActiveBreakpointMode(options.DebuggerOptions.SingleActiveBreakpoint);
            options.DebuggerOptions.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DebuggerOptions.SingleActiveBreakpoint))
                    SetSingleActiveBreakpointMode(options.DebuggerOptions.SingleActiveBreakpoint);
            };
        }

        private void SetSingleActiveBreakpointMode(bool enable)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (enable)
            {
                _pollCts = new CancellationTokenSource();
                var dte = _serviceProvider.GetService(typeof(SDTE)) as EnvDTE.DTE;
                Assumes.Present(dte);
                VSPackage.TaskFactory.RunAsyncWithErrorHandling(() => PollBreakpointsAsync(dte.Debugger, _pollCts.Token));
            }
            else
            {
                _pollCts?.Cancel();
            }
        }

        private static async Task PollBreakpointsAsync(EnvDTE.Debugger debuggerDte, CancellationToken cancellationToken)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var activeBreakpoints = new Dictionary<string, EnvDTE.Breakpoint>();
            while (!cancellationToken.IsCancellationRequested)
            {
                if (debuggerDte.Breakpoints != null)
                {
                    foreach (EnvDTE.Breakpoint newBp in debuggerDte.Breakpoints)
                    {
                        if (!newBp.Enabled)
                            continue;
                        // If the breakpoint is enabled and does not match the previously active one, disable the latter
                        if (activeBreakpoints.TryGetValue(newBp.File, out var oldBp))
                        {
                            if (newBp != oldBp)
                            {
                                try { oldBp.Enabled = false; }
                                catch (COMException) { } // the old breakpoint is deleted

                                activeBreakpoints[newBp.File] = newBp;
                            }
                        }
                        else
                        {
                            activeBreakpoints[newBp.File] = newBp;
                        }
                    }
                }
                await Task.Delay(_pollDelay);
            }
        }
    }
}
