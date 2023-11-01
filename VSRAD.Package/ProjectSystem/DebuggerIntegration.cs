using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Deborgar;
using VSRAD.Package.ProjectSystem.EditorExtensions;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    public interface IDebuggerIntegration : IEngineIntegration
    {
        event EventHandler<BreakState> BreakEntered;

        bool TryCreateDebugSession();
        void NotifyDebugActionExecuted(ActionRunResult runResult, MacroEvaluatorTransientValues transients, bool isStepping = false);
    }

    [Export(typeof(IDebuggerIntegration))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class DebuggerIntegration : IDebuggerIntegration
    {
        public event EventHandler<BreakState> BreakEntered;
        public event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;

        private readonly IProject _project;
        private readonly IActionLauncher _actionLauncher;
        private readonly IBreakpointTracker _breakpointTracker;
        private readonly IProjectSourceManager _projectSourceManager;
        private readonly BreakLineGlyphTaggerProvider _breakLineTagger;

        public bool DebugInProgress { get; private set; } = false;

        [ImportingConstructor]
        public DebuggerIntegration(IProject project, IActionLauncher actionLauncher, IBreakpointTracker breakpointTracker, IProjectSourceManager projectSourceManager)
        {
            _project = project;
            _actionLauncher = actionLauncher;
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
            _breakpointTracker.ResetToFirstBreakTarget();
            _breakLineTagger.RemoveBreakLineMarkers();

            DebugInProgress = true;
            return this;
        }

        public void DeregisterEngine()
        {
            _breakpointTracker.ResetToFirstBreakTarget();
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

        public void NotifyDebugActionExecuted(ActionRunResult runResult, MacroEvaluatorTransientValues transients, bool isStepping = false)
        {
            ExecutionCompletedEventArgs args;
            if (transients != null && transients.BreakLines.TryGetResult(out var breakLines, out _))
            {
                var breakInstances = new List<BreakInstance>();
                for (uint i = 0; i < breakLines.Length; ++i)
                    breakInstances.Add(new BreakInstance(i, new[] { ("", transients.ActiveSourceFullPath, breakLines[i]) }));
                args = new ExecutionCompletedEventArgs(breakInstances, isStepping, isSuccessful: true);
            }
            else
            {
                // Error case: if we leave the source path empty, VS debugger will open a "Source Not Available/Frame not in module" tab.
                // To avoid that, if the action execution failed and transients are not available, we attempt to pick the active file in the editor as the source.
                BreakInstance dummyInstance;
                try
                {
                    var activeEditor = _projectSourceManager.GetActiveEditorView();
                    var (path, caretLine, scrollWin) = (activeEditor.GetFilePath(), activeEditor.GetCaretPos().Line, activeEditor.GetVerticalScrollWindow());
                    var line = (caretLine >= scrollWin.FirstVisibleLine && caretLine < scrollWin.FirstVisibleLine + scrollWin.VisibleLines) ? caretLine : scrollWin.FirstVisibleLine;
                    dummyInstance = new BreakInstance(0, new[] { ("Error", path, line) });
                }
                catch
                {
                    // May throw an exception if no files are open in the editor
                    dummyInstance = new BreakInstance(0, new[] { ("Error", "", 0u) });
                }
                args = new ExecutionCompletedEventArgs(new[] { dummyInstance }, isStepping, isSuccessful: false);
            }
            ExecutionCompleted?.Invoke(this, args);
            _breakLineTagger.OnExecutionCompleted(_projectSourceManager, args);
            if (runResult?.BreakState != null)
                runResult.BreakState.BreakFile = transients.ActiveSourceFullPath;
            BreakEntered(this, runResult?.BreakState);
        }

        void IEngineIntegration.Execute(bool step)
        {
            ThreadHelper.JoinableTaskFactory.RunAsyncWithErrorHandling(async () =>
            {
                var result = await _actionLauncher.LaunchActionByNameAsync(
                    _project.Options.Profile.MenuCommands.DebugAction,
                    debugBreakTarget: step ? BreakTargetSelector.NextLine : BreakTargetSelector.NextBreakpoint);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (result.Error is Error e)
                    Errors.Show(e);
                NotifyDebugActionExecuted(result.RunResult, result.Transients, step);
            },
            exceptionCallbackOnMainThread: () => NotifyDebugActionExecuted(null, null, step));
        }

        void IEngineIntegration.CauseBreak()
        {
            NotifyDebugActionExecuted(null, null);
        }
    }
}
