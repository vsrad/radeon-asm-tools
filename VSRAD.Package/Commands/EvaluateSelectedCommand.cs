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
    [ExportCommandGroup(Constants.EvaluateSelectedCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    sealed class EvaluateSelectedCommand : BaseRemoteCommand
    {
        private readonly IProject _project;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly QuickInfoEvaluateSelectedState _quickInfoState;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly IOutputWindowManager _outputWindow;
        private readonly ICommunicationChannel _channel;

        [ImportingConstructor]
        public EvaluateSelectedCommand(
            IProject project,
            IActiveCodeEditor codeEditor,
            QuickInfoEvaluateSelectedState state,
            IFileSynchronizationManager deployManager,
            IOutputWindowManager outputWindow,
            ICommunicationChannel channel,
            SVsServiceProvider serviceProvider) : base(Constants.EvaluateSelectedCommandId, serviceProvider)
        {
            _project = project;
            _codeEditor = codeEditor;
            _quickInfoState = state;
            _deployManager = deployManager;
            _outputWindow = outputWindow;
            _channel = channel;
        }

        public override async Task RunAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var activeWord = _codeEditor.GetActiveWord();
            if (string.IsNullOrWhiteSpace(activeWord)) return;

            var breakLine = _codeEditor.GetCurrentLine() + 1;
            var watchName = activeWord.Trim();

            var evaluator = await _project.GetMacroEvaluatorAsync(breakLine, watchesOverride: new[] { watchName });
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

        private async Task<uint[]> RunAsync(DebuggerProfileOptions options)
        {
            var command = new DebugServer.IPC.Commands.Execute
            {
                Executable = options.Executable,
                Arguments = options.Arguments,
                RunAsAdministrator = options.RunAsAdmin,
                ExecutionTimeoutSecs = options.TimeoutSecs,
                WorkingDirectory = options.RemoteOutputFile.Directory
            };

            var executor = new RemoteCommandExecutor("Evaluate Selected", _channel, _outputWindow);

            await _deployManager.SynchronizeRemoteAsync().ConfigureAwait(false);

            var byteCount = 2 /* system + watch */ * 512 * 4 /* dwords -> bytes */;
            var result = await executor.ExecuteWithResultAsync(command, options.RemoteOutputFile, byteCount).ConfigureAwait(false);

            if (!result.TryGetResult(out var execResult, out var error))
                throw new Exception(error.Message);
            var (_, data) = execResult;

            var bytesFetched = new uint[1024];
            Buffer.BlockCopy(data, 0, bytesFetched, 0, data.Length);

            var values = new uint[512];
            for (uint valueIdx = 0, dwordIdx = 1 /* skip system */;
                 dwordIdx < bytesFetched.Length && valueIdx < 512;
                 valueIdx++, dwordIdx += 2 /* system + watch */)
            {
                values[valueIdx] = bytesFetched[dwordIdx];
            }
            return values;
        }
    }
}
