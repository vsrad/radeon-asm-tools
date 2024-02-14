using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    /// <summary>Created before the debug run to provide the debuggee a list of breakpoints set in the IDE and a selector for which ones will be used for this debug run.</summary>
    public sealed class BreakTarget
    {
        public static readonly BreakTarget Empty = new BreakTarget(Array.Empty<BreakpointInfo>(), BreakTargetSelector.Multiple, string.Empty, 0, string.Empty);

        public IReadOnlyList<BreakpointInfo> Breakpoints { get; }
        public BreakTargetSelector Select { get; }
        public string PrevTargetFile { get; }
        public uint PrevTargetLine { get; }

        /// <summary>
        /// (Internal to Visual Studio) The main file for this debug run, may be different from target files for breakpoints set in include files.
        /// </summary>
        [JsonIgnore]
        public string MainFile { get; }

        public BreakTarget(IReadOnlyList<BreakpointInfo> allBreakpoints, BreakTargetSelector select, string prevTargetFile, uint prevTargetLine, string mainFile)
        {
            Breakpoints = allBreakpoints;
            Select = select;
            PrevTargetFile = prevTargetFile;
            PrevTargetLine = prevTargetLine;
            MainFile = mainFile;
        }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BreakTargetSelector { Multiple, SingleRerun, SingleNext, SinglePrev, SingleStep }

    public sealed class BreakpointInfo
    {
        public string File { get; }
        public uint Line { get; }
        public uint HitCountTarget { get; }
        public uint StopOnHit { get; }

        [JsonIgnore]
        public string Location => $"{System.IO.Path.GetFileName(File)}:{Line + 1}";

        public BreakpointInfo(string file, uint line, uint hitCountTarget, bool stopOnHit)
        {
            File = file;
            Line = line;
            HitCountTarget = hitCountTarget;
            StopOnHit = stopOnHit ? 1u : 0u;
        }
    }

    public interface IBreakpointTracker
    {
        Result<BreakTarget> GetTarget(string mainFile, BreakTargetSelector mode);
        void UpdateOnBreak(BreakTarget breakTarget, IReadOnlyList<BreakpointInfo> validBreakpoints);
        void SetRunToLine(string mainFile, uint line);
        void ResetTargets();
    }

    [Export(typeof(IBreakpointTracker))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BreakpointTracker : IBreakpointTracker
    {
        /// <summary>
        /// Indexed by main source file (breakpoint file may be different if set in include file); paths are case-insensitive as we're on Windows.
        /// Break location should be read from the Breakpoint field if it is non-null, as it tracks line changes due to edits in the source file.
        /// The Breakpoint field may be null if the previous run target is not a valid breakpoint, in which case the location should be read from the File and Line fields.
        /// </summary>
        private readonly Dictionary<string, (string File, uint Line, Breakpoint2 Breakpoint, bool ForceRunToLine)> _lastTarget =
            new Dictionary<string, (string, uint, Breakpoint2, bool)>(StringComparer.OrdinalIgnoreCase);

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
            _lastTarget[file] = (File: file, Line: line, Breakpoint: null, ForceRunToLine: true);
        }

        public void ResetTargets()
        {
            _lastTarget.Clear();
        }

        public Result<BreakTarget> GetTarget(string mainFile, BreakTargetSelector selector)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var (hitCountTarget, stopOnHit) = (_projectOptions.DebuggerOptions.Counter, _projectOptions.DebuggerOptions.StopOnHit);
            var (prevTargetFile, prevTargetLine) = ("", 0u);
            if (_lastTarget.TryGetValue(mainFile, out var lt))
            {
                if (lt.ForceRunToLine)
                    return new BreakTarget(new[] { new BreakpointInfo(lt.File, lt.Line, hitCountTarget, stopOnHit) }, BreakTargetSelector.SingleNext, "", 0u, mainFile);

                if (lt.Breakpoint != null)
                {
                    try
                    {
                        (prevTargetFile, prevTargetLine) = (lt.Breakpoint.File, (uint)lt.Breakpoint.FileLine - 1);
                    }
                    catch (System.Runtime.InteropServices.COMException) // DTE breakpoint is no longer valid (has been removed)
                    {
                        (prevTargetFile, prevTargetLine) = (lt.File, lt.Line);
                    }
                }
                else
                {
                    (prevTargetFile, prevTargetLine) = (lt.File, lt.Line);
                }
            }
            switch (selector)
            {
                // Implemented by debuggee
                case BreakTargetSelector.Multiple:
                case BreakTargetSelector.SingleRerun:
                case BreakTargetSelector.SingleNext:
                case BreakTargetSelector.SinglePrev:
                    {
                        var breakpoints = new List<BreakpointInfo>();
                        foreach (Breakpoint2 bp in _dte.Debugger.Breakpoints)
                            if (bp.Enabled && !string.IsNullOrEmpty(bp.File))
                                breakpoints.Add(new BreakpointInfo(bp.File, (uint)bp.FileLine - 1, hitCountTarget, stopOnHit));

                        if (breakpoints.Count == 0)
                            return new Error($"No breakpoints set\n\nSource file: {mainFile}");
                        else
                            return new BreakTarget(breakpoints, selector, prevTargetFile, prevTargetLine, mainFile);
                    }
                // Emulated
                case BreakTargetSelector.SingleStep:
                    {
                        if (string.IsNullOrEmpty(prevTargetFile))
                            return new Error($"Cannot step until a breakpoint has been hit\n\nSource file: {mainFile}");
                        return new BreakTarget(new[] { new BreakpointInfo(lt.File, lt.Line + 1, hitCountTarget, stopOnHit) }, BreakTargetSelector.SingleNext, "", 0u, mainFile);
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        public void UpdateOnBreak(BreakTarget breakTarget, IReadOnlyList<BreakpointInfo> validBreakpoints)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (breakTarget.Select != BreakTargetSelector.Multiple && validBreakpoints.Count == 1 && validBreakpoints[0] is BreakpointInfo breakpoint)
            {
                Breakpoint2 dteBreakpoint = _dte.Debugger.Breakpoints.Cast<Breakpoint2>()
                    .FirstOrDefault(bp => bp.Enabled && breakpoint.Line == (uint)bp.FileLine - 1 && string.Equals(breakpoint.File, bp.File, StringComparison.OrdinalIgnoreCase));

                _lastTarget[breakTarget.MainFile] = (File: breakpoint.File, Line: breakpoint.Line, Breakpoint: dteBreakpoint, ForceRunToLine: false);
            }
        }
    }
}
