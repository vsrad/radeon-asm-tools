using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Deborgar;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem
{
    public interface IBreakpointTracker
    {
        BreakTarget MoveToNextBreakTarget(string file, bool step);
        uint[] GetBreakTargetLines(string file);
        void RunToLine(string file, uint line);
    }

    [Export(typeof(IBreakpointTracker))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BreakpointTracker : IBreakpointTracker
    {
        private readonly Dictionary<string, BreakTarget> _breakTargets = new Dictionary<string, BreakTarget>();

        private DTE _dte;
        private ProjectOptions _projectOptions;

        private BreakTarget _runToLine;

        [ImportingConstructor]
        public BreakpointTracker(IProject project, SVsServiceProvider serviceProvider)
        {
            project.Loaded += (options) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
                Assumes.Present(_dte);

                _projectOptions = options;
            };
        }

        public void RunToLine(string file, uint line)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _runToLine = new BreakTarget(file, new[] { line }, isStepping: false);

            // Start debugging (F5)
            if (_dte.Debugger.CurrentMode != dbgDebugMode.dbgRunMode) // Go() must not be invoked when the debugger is already running (not in break mode)
                _dte.Debugger.Go();
        }

        public BreakTarget MoveToNextBreakTarget(string file, bool step)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            BreakTarget target;
            if (step)
            {
                if (_breakTargets.TryGetValue(file, out var prevTarget))
                {
                    if (_projectOptions.DebuggerOptions.BreakMode == BreakMode.Multiple)
                        target = new BreakTarget(file, prevTarget.Lines, isStepping: true);
                    else
                        target = new BreakTarget(file, new[] { prevTarget.Lines[0] + 1 }, isStepping: true);
                }
                else
                {
                    target = new BreakTarget(file, new[] { 0u }, isStepping: true);
                }
            }
            else
            {
                target = new BreakTarget(file, GetBreakTargetLines(file), isStepping: true);
                _runToLine = null;
            }
            _breakTargets[file] = target;
            return target;
        }

        public uint[] GetBreakTargetLines(string file)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_runToLine is BreakTarget runToLineTarget)
                return runToLineTarget.Lines;

            var breakpointLines = new List<uint>();
            foreach (EnvDTE.Breakpoint bp in _dte.Debugger.Breakpoints)
                if (bp.Enabled && bp.File == file)
                    breakpointLines.Add((uint)bp.FileLine - 1);
            breakpointLines.Sort();

            if (breakpointLines.Count == 0)
            {
                // No breakpoints set but we need to pass one to the debugger anyway,
                // so we pick the end of the source file as the implicit "default" breakpoint
                var lastLine = (uint)(File.ReadLines(file).Count() - 1);
                breakpointLines.Add(lastLine);
            }

            switch (_projectOptions.DebuggerOptions.BreakMode)
            {
                case BreakMode.Multiple:
                    return breakpointLines.ToArray();
                case BreakMode.SingleRerun:
                    if (_breakTargets.TryGetValue(file, out var prevTarget))
                        return prevTarget.Lines;

                    return new[] { breakpointLines[0] };
                default:
                    var previousBreakLine = _breakTargets.TryGetValue(file, out prevTarget) ? prevTarget.Lines[0] : 0;

                    foreach (var breakLine in breakpointLines)
                        if (breakLine > previousBreakLine)
                            return new[] { breakLine };

                    return new[] { breakpointLines[0] };
            }
        }
    }
}
