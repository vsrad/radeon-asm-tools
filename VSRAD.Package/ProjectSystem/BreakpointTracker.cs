using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    public enum BreakTargetSelector { Last, NextBreakpoint, NextLine }

    public interface IBreakpointTracker
    {
        Result<uint[]> GoToBreakTarget(string file, BreakTargetSelector selector);
        void SetRunToLine(string file, uint line);
        void ResetToFirstBreakTarget();
    }

    [Export(typeof(IBreakpointTracker))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BreakpointTracker : IBreakpointTracker
    {
        private readonly Dictionary<string, (uint Line, Breakpoint2 Breakpoint, bool ForceRunToLine)> _lastTarget =
            new Dictionary<string, (uint, Breakpoint2, bool)>();

        private DTE2 _dte;
        private ProjectOptions _projectOptions;

        [ImportingConstructor]
        public BreakpointTracker(IProject project, SVsServiceProvider serviceProvider)
        {
            project.RunWhenLoaded((options) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _dte = serviceProvider.GetService(typeof(SDTE)) as DTE2;
                Assumes.Present(_dte);

                _projectOptions = options;
            });
        }

        public void SetRunToLine(string file, uint line)
        {
            _lastTarget[file] = (Line: line, Breakpoint: null, ForceRunToLine: true);
        }

        public void ResetToFirstBreakTarget()
        {
            _lastTarget.Clear();
        }

        public Result<uint[]> GoToBreakTarget(string file, BreakTargetSelector selector)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_lastTarget.TryGetValue(file, out var lt) && lt.ForceRunToLine)
            {
                _lastTarget[file] = (lt.Line, Breakpoint: null, ForceRunToLine: false);
                return new[] { GetBreakTarget(lt.Line) };
            }

            var breakpoints = _dte.Debugger.Breakpoints.Cast<Breakpoint2>().Where(bp => bp.Enabled && bp.File == file).ToList();
            breakpoints.Sort((a, b) => a.FileLine.CompareTo(b.FileLine));
            if (breakpoints.Count == 0)
                return new Error($"No breakpoints set\n\nSource file: {file}");

            if (_projectOptions.DebuggerOptions.EnableMultipleBreakpoints)
                return breakpoints.Select(GetBreakTarget).ToArray();

            switch (selector)
            {
                case BreakTargetSelector.Last:
                    if (_lastTarget.TryGetValue(file, out lt))
                    {
                        if (breakpoints.Contains(lt.Breakpoint))
                            return new[] { GetBreakTarget(lt.Breakpoint) };
                        else if (lt.Breakpoint == null) // stepping
                            return new[] { GetBreakTarget(lt.Line) };
                    }
                    goto case BreakTargetSelector.NextBreakpoint;
                case BreakTargetSelector.NextBreakpoint:
                    Breakpoint2 nextBreakpoint = null;
                    if (_lastTarget.TryGetValue(file, out lt))
                    {
                        int lastIdx;
                        if (lt.Breakpoint != null && (lastIdx = breakpoints.IndexOf(lt.Breakpoint)) != -1)
                            nextBreakpoint = breakpoints[(lastIdx + 1) % breakpoints.Count];
                        else
                            nextBreakpoint = breakpoints.Find(b => ((uint)b.FileLine - 1) > lt.Line);
                    }
                    nextBreakpoint = nextBreakpoint ?? breakpoints[0];
                    _lastTarget[file] = (Line: (uint)nextBreakpoint.FileLine - 1, Breakpoint: nextBreakpoint, ForceRunToLine: false);
                    return new[] { GetBreakTarget(nextBreakpoint) };
                case BreakTargetSelector.NextLine:
                    if (_lastTarget.TryGetValue(file, out lt))
                    {
                        _lastTarget[file] = (lt.Line + 1, Breakpoint: null, ForceRunToLine: false);
                        return new[] { GetBreakTarget(lt.Line + 1) };
                    }
                    return new Error($"Stepping is not available until a breakpoint has been hit.\n\nSource file: {file}");
                default:
                    return new Error("Undefined break target selector.");
            }
        }

        private uint GetBreakTarget(Breakpoint2 breakpoint) =>
            (uint)breakpoint.FileLine - 1;

        private uint GetBreakTarget(uint line) =>
            line;
    }
}
