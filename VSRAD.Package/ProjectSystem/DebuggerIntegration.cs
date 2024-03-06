using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Deborgar;
using VSRAD.Package.ProjectSystem.EditorExtensions;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    public interface IDebuggerIntegration : IEngineIntegration
    {
        event EventHandler<Result<BreakState>> BreakEntered;

        bool TryCreateDebugSession();
        void NotifyDebugActionExecuted(Result<ActionRunResult> actionRun, BreakTargetSelector breakTarget);
    }

    [Export(typeof(IDebuggerIntegration))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class DebuggerIntegration : IDebuggerIntegration
    {
        public event EventHandler<Result<BreakState>> BreakEntered;
        public event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;

        private readonly IProject _project;
        private readonly Lazy<IActionController> _actionController;
        private readonly IBreakpointTracker _breakpointTracker;
        private readonly IProjectSourceManager _projectSourceManager;
        private readonly BreakLineGlyphTaggerProvider _breakLineTagger;

        public bool DebugInProgress { get; private set; } = false;

        [ImportingConstructor]
        public DebuggerIntegration(
            IProject project,
            Lazy<IActionController> actionController, // Must be imported lazily due to circular dependency
            IBreakpointTracker breakpointTracker,
            IProjectSourceManager projectSourceManager)
        {
            _project = project;
            _actionController = actionController;
            _breakpointTracker = breakpointTracker;
            _projectSourceManager = projectSourceManager;

            // Cannot import BreakLineGlyphTaggerProvider directly because there are
            // multiple IViewTaggerProvider exports and we don't want to instantiate each one
            _breakLineTagger = (BreakLineGlyphTaggerProvider)
                _project.GetExportByMetadataAndType<IViewTaggerProvider, IAppliesToMetadataView>(
                        m => m.AppliesTo == Constants.RadOrVisualCProjectCapability,
                        e => e.GetType() == typeof(BreakLineGlyphTaggerProvider));
        }

        public IEngineIntegration RegisterEngine()
        {
            if (DebugInProgress)
                throw new InvalidOperationException($"{nameof(RegisterEngine)} must only be called by the engine, and the engine must be launched via {nameof(DebuggerLaunchProvider)}");

            // When entering the debug mode, we always want to start from the first breakpoint. The current next break target
            // may be different, however, because the debug action may have been run in the edit mode, so we need to reset the state.
            _breakpointTracker.ResetTargets();
            _breakLineTagger.RemoveBreakLineMarkers();

            DebugInProgress = true;
            return this;
        }

        public void DeregisterEngine()
        {
            _breakpointTracker.ResetTargets();
            _breakLineTagger.RemoveBreakLineMarkers();

            DebugInProgress = false;
        }

        public bool TryCreateDebugSession()
        {
            if (!_project.Options.HasProfiles)
            {
                Errors.ShowProfileUninitializedError();
                return false;
            }

            DebugEngine.InitializationCallback = RegisterEngine;
            DebugEngine.TerminationCallback = DeregisterEngine;

            return true;
        }

        public void NotifyDebugActionExecuted(Result<ActionRunResult> actionRun, BreakTargetSelector breakTarget)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Result<BreakState> breakResult;
            var validBreakpoints = new List<BreakpointInfo>();
            var hitLocations = new List<BreakLocation>();
            if (actionRun.TryGetResult(out var runResult, out _) && runResult?.BreakState is BreakState breakState)
            {
                foreach (int breakpointIdx in breakState.BreakpointIndexPerInstance.Values.Distinct())
                    validBreakpoints.Add(breakState.Target.Breakpoints[breakpointIdx]);
                _breakpointTracker.UpdateOnBreak(breakState.Target, validBreakpoints);

                foreach (var breakpointIdx in breakState.HitBreakpoints)
                {
                    var breakpoint = breakState.Target.Breakpoints[(int)breakpointIdx];
                    hitLocations.Add(new BreakLocation(breakpointIdx, new[] { ("", breakpoint.File, breakpoint.Line) }));
                }

                breakResult = breakState;
            }
            else
            {
                breakResult = new Error("Run failed, see the Output window for more details");
            }

            var isStepping = breakTarget == BreakTargetSelector.SingleStep;
            ExecutionCompletedEventArgs args;
            if (hitLocations.Count > 0)
            {
                args = new ExecutionCompletedEventArgs(hitLocations, isStepping, isSuccessful: true);
            }
            else
            {
                BreakLocation errorLocation;
                if (validBreakpoints.Count > 0)
                {
                    errorLocation = new BreakLocation(0, new[] { ("No breakpoints hit", validBreakpoints[0].File, validBreakpoints[0].Line) });
                }
                else
                {
                    // If we leave the source path empty, VS debugger will open a "Source Not Available/Frame not in module" tab.
                    // To avoid that, if the action execution failed and transients are not available, we attempt to pick the active file in the editor as the source.
                    string errorPath;
                    uint errorLine;
                    try
                    {
                        var activeEditor = _projectSourceManager.GetActiveEditorView();
                        errorPath = activeEditor.GetFilePath();
                        var (caretLine, scrollWin) = (activeEditor.GetCaretPos().Line, activeEditor.GetVerticalScrollWindow());
                        errorLine = (caretLine >= scrollWin.FirstVisibleLine && caretLine < scrollWin.FirstVisibleLine + scrollWin.VisibleLines) ? caretLine : scrollWin.FirstVisibleLine;
                    }
                    catch
                    {
                        // May throw an exception if no files are open in the editor
                        (errorPath, errorLine) = ("", 0u);
                    }
                    errorLocation = new BreakLocation(0, new[] { ("Error", errorPath, errorLine) });
                }
                args = new ExecutionCompletedEventArgs(new[] { errorLocation }, isStepping, isSuccessful: false);
            }

            // Notify VS debugger that we stopped at a breakpoint, do this first so we can override debugger behavior in later events
            ExecutionCompleted?.Invoke(this, args);
            // VS debugger (via ExecutionCompleted) will navigate to the break location when using F5, but for Rerun Debug and Reverse Debug we need to do it ourselves
            _projectSourceManager.OpenDocument(args.BreakLocations[0].CallStack[0].SourcePath, args.BreakLocations[0].CallStack[0].SourceLine);
            // Update Visualizer after navigating to the break line
            BreakEntered?.Invoke(this, breakResult);
            // Make the Visualizer window active
            VSPackage.VisualizerToolWindow?.BringToFront();
            // Finally, override VS debugger break line markers
            _breakLineTagger.OnExecutionCompleted(_projectSourceManager, args);
        }

        void IEngineIntegration.Execute(bool step)
        {
            var debugBreakTarget = _project.Options.DebuggerOptions.EnableMultipleBreakpoints ? BreakTargetSelector.Multiple
                : step ? BreakTargetSelector.SingleStep
                : BreakTargetSelector.SingleNext;
            ThreadHelper.JoinableTaskFactory.RunAsyncWithErrorHandling(() =>
                _actionController.Value.RunActionAsync(_project.Options.Profile.MenuCommands.DebugAction, debugBreakTarget));
        }

        void IEngineIntegration.CauseBreak()
        {
            _actionController.Value.AbortRunningAction();
            NotifyDebugActionExecuted((ActionRunResult)null, BreakTargetSelector.SingleNext);
        }
    }
}
