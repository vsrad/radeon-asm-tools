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
    public enum BreakTargetSelector { Last, NextBreakpoint, PrevBreakpoint, NextLine }

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
                return GoToTarget(file, lt.Line);

            var breakpoints = _dte.Debugger.Breakpoints.Cast<Breakpoint2>().Where(bp => bp.Enabled && bp.File == file).ToList();
            breakpoints.Sort((a, b) => a.FileLine.CompareTo(b.FileLine));
            if (breakpoints.Count == 0)
                return new Error($"No breakpoints set\n\nSource file: {file}");

            if (_projectOptions.DebuggerOptions.EnableMultipleBreakpoints)
                return GoToTarget(file, breakpoints);

            switch (selector)
            {
                case BreakTargetSelector.Last:
                    if (_lastTarget.TryGetValue(file, out lt))
                    {
                        if (breakpoints.Contains(lt.Breakpoint))
                            return GoToTarget(file, lt.Breakpoint);
                        else if (lt.Breakpoint == null) // stepping
                            return GoToTarget(file, lt.Line);
                    }
                    goto case BreakTargetSelector.NextBreakpoint;
                case BreakTargetSelector.NextBreakpoint:
                    if (_lastTarget.TryGetValue(file, out lt))
                    {
                        if (lt.Breakpoint != null && breakpoints.IndexOf(lt.Breakpoint) is int lastIdx && lastIdx != -1)
                            return GoToTarget(file, breakpoints[(lastIdx + 1) % breakpoints.Count]);
                        else
                            return GoToTarget(file, breakpoints.Find(b => ((uint)b.FileLine - 1) > lt.Line) ?? breakpoints[0]);
                    }
                    return GoToTarget(file, breakpoints[0]);
                case BreakTargetSelector.PrevBreakpoint:
                    if (_lastTarget.TryGetValue(file, out lt))
                    {
                        if (lt.Breakpoint != null && breakpoints.IndexOf(lt.Breakpoint) is int lastIdx && lastIdx != -1)
                            return GoToTarget(file, breakpoints[lastIdx == 0 ? breakpoints.Count - 1 : lastIdx - 1]);
                        else
                            return GoToTarget(file, breakpoints.Find(b => ((uint)b.FileLine - 1) < lt.Line) ?? breakpoints[breakpoints.Count - 1]);
                    }
                    return GoToTarget(file, breakpoints[breakpoints.Count - 1]);
                case BreakTargetSelector.NextLine:
                    if (_lastTarget.TryGetValue(file, out lt))
                        return GoToTarget(file, lt.Line + 1);
                    return new Error($"Stepping is not available until a breakpoint has been hit.\n\nSource file: {file}");
                default:
                    return new Error("Undefined break target selector.");
            }
        }

        private uint[] GoToTarget(string file, uint targetLine)
        {
            _lastTarget[file] = (Line: targetLine, Breakpoint: null, ForceRunToLine: false);
            return new[] { targetLine };
        }

        private uint[] GoToTarget(string file, Breakpoint2 targetBreakpoint)
        {
            _lastTarget[file] = (Line: (uint)targetBreakpoint.FileLine - 1, Breakpoint: targetBreakpoint, ForceRunToLine: false);
            return new[] { (uint)targetBreakpoint.FileLine - 1 };
        }

        private uint[] GoToTarget(string file, IEnumerable<Breakpoint2> multipleBreakpoints)
        {
            return multipleBreakpoints.Select(b => (uint)b.FileLine - 1).ToArray();
        }
    }
}
