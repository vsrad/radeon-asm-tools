using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.EditorExtensions;
using VSRAD.Package.Server;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    //[Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    sealed class EvaluateSelectedCommand : BaseRemoteCommand
    {
        private readonly IProject _project;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly QuickInfoEvaluateSelectedState _quickInfoState;
#pragma warning disable IDE0052 // Remove unread private members
        private readonly IFileSynchronizationManager _deployManager;
        private readonly IOutputWindowManager _outputWindow;
        private readonly ICommunicationChannel _channel;
        private readonly IErrorListManager _errorListManager;
#pragma warning restore IDE0052 // Remove unread private members

        [ImportingConstructor]
        public EvaluateSelectedCommand(
            IProject project,
            IActiveCodeEditor codeEditor,
            QuickInfoEvaluateSelectedState state,
            IFileSynchronizationManager deployManager,
            IOutputWindowManager outputWindow,
            ICommunicationChannel channel,
            SVsServiceProvider serviceProvider,
            IErrorListManager errorListManager) : base(Constants.EvaluateSelectedCommandSet, Constants.EvaluateSelectedCommandId, serviceProvider)
        {
            _project = project;
            _codeEditor = codeEditor;
            _quickInfoState = state;
            _deployManager = deployManager;
            _outputWindow = outputWindow;
            _channel = channel;
            _errorListManager = errorListManager;
        }

        public override async Task RunAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var activeWord = _codeEditor.GetActiveWord();
            if (string.IsNullOrWhiteSpace(activeWord))
                return;

            var breakLine = _codeEditor.GetCurrentLine() + 1;
            var watchName = activeWord.Trim();

            var evaluator = await _project.GetMacroEvaluatorAsync(new[] { breakLine }, watchesOverride: new[] { watchName });
            var options = await _project.Options.Profile.Debugger.EvaluateAsync(evaluator);
            await SetStatusBarTextAsync($"RAD Debug: Evaluating {watchName}...");
            try
            {
                var data = await RunAsync(options);
                if (data == null)
                {
                    Errors.ShowCritical($"Please add {watchName} to watches and start a regular debug session.", $"Unable to evaluate {watchName}");
                }
                else
                {
                    _quickInfoState.SetEvaluatedData(watchName, data);
                    if (_quickInfoState.TryGetEvaluated(watchName, out var values))
                    {
                        VSPackage.EvaluateSelectedWindow.EvaluateSelectedControl.UpdateWatches(watchName, values);
                        (VSPackage.EvaluateSelectedWindow.Frame as IVsWindowFrame).Show();
                    }
                }
            }
            finally
            {
                await ClearStatusBarAsync();
            }
        }

        private Task<uint[]> RunAsync(DebuggerProfileOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
