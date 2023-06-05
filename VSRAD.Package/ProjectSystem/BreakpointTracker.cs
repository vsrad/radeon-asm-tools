using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;
using System.Linq;
using EnvDTE80;
using Newtonsoft.Json;

namespace VSRAD.Package.ProjectSystem
{
    class BreakpointState
    {
        public bool Resumable { get; set; }

        [JsonConstructor]
        public BreakpointState(bool resumable)
        {
            Resumable = resumable;
        }
    }

    public interface IBreakpointTracker
    {
        Result<(uint BreakLine, bool Resumable)[]> MoveToNextBreakTarget(string file, bool step);
        Result<(uint BreakLine, bool Resumable)[]> GetBreakTarget(string file, bool step);
        void SetRunToLine(string file, uint line);
        void ResetToFirstBreakTarget();
        void SetResumableState(string file, uint line, bool resumable);
        bool GetResumableState(string file, uint line);
    }

    [Export(typeof(IBreakpointTracker))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BreakpointTracker : IBreakpointTracker
    {
        private readonly Dictionary<string, (uint Line, bool ForceRunToLine)> _breakTargetPerFile = new Dictionary<string, (uint, bool)>();

        private DTE _dte;
        private ProjectOptions _projectOptions;


        [ImportingConstructor]
        public BreakpointTracker(IProject project, SVsServiceProvider serviceProvider)
        {
            project.RunWhenLoaded((options) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
                Assumes.Present(_dte);

                _projectOptions = options;
            });
        }

        public void SetRunToLine(string file, uint line)
        {
            _breakTargetPerFile[file] = (Line: line, ForceRunToLine: true);
        }

        public void ResetToFirstBreakTarget()
        {
            _breakTargetPerFile.Clear();
        }

        public Result<(uint BreakLine, bool Resumable)[]> MoveToNextBreakTarget(string file, bool step)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var nextTargetResult = GetBreakTarget(file, step);
            if (nextTargetResult.TryGetResult(out var nextTarget, out _))
            {
                if (_projectOptions.DebuggerOptions.BreakMode == BreakMode.Multiple)
                    _breakTargetPerFile.Remove(file);
                else
                    _breakTargetPerFile[file] = (Line: nextTarget[0].BreakLine, ForceRunToLine: false);
            }
            return nextTargetResult;
        }

        public Result<(uint BreakLine, bool Resumable)[]> GetBreakTarget(string file, bool step)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_breakTargetPerFile.TryGetValue(file, out var target) && target.ForceRunToLine)
            {
                return new[] { (target.Line, GetResumableState(file, target.Line)) };
            }
            else if (step)
            {
                if (_projectOptions.DebuggerOptions.BreakMode == BreakMode.Multiple)
                    return new Error("Stepping is not supported in multiple breakpoints mode.");

                if (_breakTargetPerFile.TryGetValue(file, out var prevTarget))
                    return new[] { (prevTarget.Line + 1, GetResumableState(file, prevTarget.Line + 1)) };
                else
                    return new Error($"Stepping is not available until a breakpoint has been hit in the current file ({file}).");
            }
            else
            {
                var breakpointLines = new List<uint>();
                foreach (Breakpoint bp in _dte.Debugger.Breakpoints)
                    if (bp.Enabled && bp.File == file)
                        breakpointLines.Add((uint)bp.FileLine - 1);
                breakpointLines.Sort();

                if (breakpointLines.Count == 0)
                    return new Error($"No breakpoints are set in the current file ({file}).");

                switch (_projectOptions.DebuggerOptions.BreakMode)
                {
                    case BreakMode.Multiple:
                        return breakpointLines.Select(line => (line, GetResumableState(file, line))).ToArray();
                    case BreakMode.SingleRerun:
                        if (_breakTargetPerFile.TryGetValue(file, out var prevTarget) && breakpointLines.Contains(prevTarget.Line))
                            return new[] { (prevTarget.Line, GetResumableState(file, prevTarget.Line)) };
                        else
                            goto case BreakMode.SingleRoundRobin;
                    case BreakMode.SingleRoundRobin:
                        var previousBreakLine = _breakTargetPerFile.TryGetValue(file, out prevTarget) ? prevTarget.Line : 0;
                        foreach (var breakLine in breakpointLines)
                            if (breakLine > previousBreakLine)
                                return new[] { (breakLine, GetResumableState(file, breakLine)) };
                        return new[] { (breakpointLines[0], GetResumableState(file, breakpointLines[0])) };
                    default:
                        return new Error("Undefined breakpoint mode.");
                }
            }
        }

        public void SetResumableState(string file, uint line, bool resumable)
        {
            EnvDTE80.Debugger2 debugger = (EnvDTE80.Debugger2)_dte.Debugger;
            var breakpoint = debugger.Breakpoints.Cast<EnvDTE80.Breakpoint2>().FirstOrDefault(br => br.File == file && (br.FileLine - 1) == line);
            if (breakpoint != null)
            {
                BreakpointState breakpointState;
                if (!string.IsNullOrEmpty(breakpoint.Tag))
                {
                    breakpointState = JsonConvert.DeserializeObject<BreakpointState>(breakpoint.Tag);
                    breakpointState.Resumable = resumable;
                }
                else
                {
                    breakpointState = new BreakpointState(resumable);
                }

                string newTag = JsonConvert.SerializeObject(breakpointState, Formatting.None);
                breakpoint.Tag = newTag;
            }
            else
            {
                Errors.ShowWarning("Breakpoint not found at " + file + ":" + line);
            }


        }

        public bool GetResumableState(string file, uint line)
        {
            EnvDTE80.Debugger2 debugger = (EnvDTE80.Debugger2)_dte.Debugger;
            var breakpoint = debugger.Breakpoints.Cast<EnvDTE80.Breakpoint2>().FirstOrDefault(br => br.File == file && (br.FileLine - 1) == line);
            if (breakpoint != null)
            {
                BreakpointState breakpointState;
                if (!string.IsNullOrEmpty(breakpoint.Tag))
                {
                    breakpointState = JsonConvert.DeserializeObject<BreakpointState>(breakpoint.Tag);
                    return breakpointState.Resumable;
                }
                else
                {
                    SetResumableState(file, line, _projectOptions.DebuggerOptions.ResumableDefaultValue);
                    return _projectOptions.DebuggerOptions.ResumableDefaultValue;
                }
            }
            else
            {
                return _projectOptions.DebuggerOptions.ResumableDefaultValue;
            }
        }

    }


}
