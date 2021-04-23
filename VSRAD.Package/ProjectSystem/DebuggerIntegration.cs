using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.ComponentModel.Composition;
using VSRAD.Deborgar;
using VSRAD.Package.ProjectSystem.EditorExtensions;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    [Export]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class DebuggerIntegration : IEngineIntegration
    {
        public event EventHandler<BreakState> BreakEntered;
        public event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;

        private readonly IProject _project;
        private readonly IActionLauncher _actionLauncher;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly IBreakpointTracker _breakpointTracker;
        private readonly BreakLineGlyphTaggerProvider _breakLineTagger;

        public bool DebugInProgress { get; private set; } = false;

        [ImportingConstructor]
        public DebuggerIntegration(IProject project, IActionLauncher actionLauncher, IActiveCodeEditor codeEditor, IBreakpointTracker breakpointTracker)
        {
            _project = project;
            _actionLauncher = actionLauncher;
            _actionLauncher.ActionCompleted += ActionCompleted;
            _codeEditor = codeEditor;
            _breakpointTracker = breakpointTracker;

            // Cannot import BreakLineGlyphTaggerProvider directly because there are
            // multiple IViewTaggerProvider exports and we don't want to instantiate each one
            _breakLineTagger = (BreakLineGlyphTaggerProvider)
                _project.GetExportByMetadataAndType<IViewTaggerProvider, IAppliesToMetadataView>(
                        m => m.AppliesTo == Constants.RadOrVisualCProjectCapability,
                        e => e.GetType() == typeof(BreakLineGlyphTaggerProvider));
        }

        private void ActionCompleted(object sender, ActionCompletedEventArgs e)
        {
            if (e.Error is Error error)
                Errors.Show(error);

            if (e.RunResult == null) // RunResult is null when an error has occurred
                return;

            if (_actionLauncher.IsDebugAction(e.Action))
                RaiseExecutionCompleted(e.Transients.ActiveSourceFullPath ?? "", e.Transients.BreakLines ?? new[] { 0u }, isStepping: false, e.RunResult.BreakState);
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

        public void Execute(bool step)
        {
            var error = _actionLauncher.TryLaunchActionByName(_project.Options.Profile.MenuCommands.DebugAction, moveToNextDebugTarget: true, isDebugSteppingEnabled: step);
            if (error is Error e)
                Errors.Show(e);
        }

        void IEngineIntegration.CauseBreak()
        {
            string file = "";
            // May throw an exception if no files are open in the editor
            try { file = _codeEditor.GetAbsoluteSourcePath(); } catch { }

            RaiseExecutionCompleted(file, new[] { 0u }, isStepping: false, null);
        }

        private void RaiseExecutionCompleted(string file, uint[] lines, bool isStepping, BreakState breakState)
        {
            var args = new ExecutionCompletedEventArgs(file, lines, isStepping);
            ExecutionCompleted?.Invoke(this, args);
            _breakLineTagger.OnExecutionCompleted(args);
            BreakEntered(this, breakState);
        }
    }
}
