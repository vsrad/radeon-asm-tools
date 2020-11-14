using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem
{
    public interface IBreakpointTracker
    {
        (string, uint[]) MoveToNextBreakTarget(bool step);
        (string, uint[]) GetBreakTarget();
        void RunToLine(string file, uint line);
    }

    [Export(typeof(IBreakpointTracker))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BreakpointTracker : IBreakpointTracker
    {
        private readonly Dictionary<string, uint[]> _breakTargets = new Dictionary<string, uint[]>();
        private readonly IActiveCodeEditor _codeEditor;

        private DTE _dte;
        private ProjectOptions _projectOptions;

        private (string, uint[])? _runToLine;

        [ImportingConstructor]
        public BreakpointTracker(IProject project, IActiveCodeEditor codeEditor, SVsServiceProvider serviceProvider)
        {
            _codeEditor = codeEditor;
            project.RunWhenLoaded((options) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
                Assumes.Present(_dte);

                _projectOptions = options;
            });
        }

        public void RunToLine(string file, uint line)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _runToLine = (file, new[] { line });

            // Start debugging (F5)
            if (_dte.Debugger.CurrentMode != dbgDebugMode.dbgRunMode) // Go() must not be invoked when the debugger is already running (not in break mode)
                _dte.Debugger.Go();
        }

        public (string, uint[]) MoveToNextBreakTarget(bool step)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            (string, uint[]) target;

            if (step && _projectOptions.DebuggerOptions.BreakMode != BreakMode.Multiple)
            {
                var file = _codeEditor.GetAbsoluteSourcePath();
                if (_breakTargets.TryGetValue(file, out var prevTarget))
                    target = (file, new[] { prevTarget[0] + 1 });
                else
                    target = (file, new[] { 0u });
            }
            else
            {
                target = GetBreakTarget();
                _runToLine = null;
            }

            _breakTargets[target.Item1] = target.Item2;
            return target;
        }

        public (string, uint[]) GetBreakTarget()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_runToLine.HasValue)
                return _runToLine.Value;

            var file = _codeEditor.GetAbsoluteSourcePath();

            var breakpointLines = new List<uint>();
            foreach (Breakpoint bp in _dte.Debugger.Breakpoints)
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
                    return (file, breakpointLines.ToArray());
                case BreakMode.SingleRerun:
                    if (_breakTargets.TryGetValue(file, out var prevTarget))
                        return (file, prevTarget);

                    return (file, new[] { breakpointLines[0] });
                default:
                    var previousBreakLine = _breakTargets.TryGetValue(file, out prevTarget) ? prevTarget[0] : 0;

                    foreach (var breakLine in breakpointLines)
                        if (breakLine > previousBreakLine)
                            return (file, new[] { breakLine });

                    return (file, new[] { breakpointLines[0] });
            }
        }
    }
}
