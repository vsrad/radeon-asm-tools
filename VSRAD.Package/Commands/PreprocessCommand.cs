using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.IO;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.PreprocessCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class PreprocessCommand : BaseRemoteCommand
    {
        private readonly IProject _project;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly IOutputWindowManager _outputWindow;
        private readonly ICommunicationChannel _channel;

        [ImportingConstructor]
        public PreprocessCommand(
            IProject project,
            IFileSynchronizationManager deployManager,
            IOutputWindowManager outputWindow,
            ICommunicationChannel channel,
            SVsServiceProvider serviceProvider) : base(Constants.PreprocessCommandId, serviceProvider)
        {
            _project = project;
            _deployManager = deployManager;
            _outputWindow = outputWindow;
            _channel = channel;
        }

        public override async Task RunAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.Preprocessor.EvaluateAsync(evaluator);
            var command = new Execute { Executable = options.Executable, Arguments = options.Arguments, WorkingDirectory = options.WorkingDirectory, RunAsAdministrator = false };

            await SetStatusBarTextAsync("RAD Debug: Preprocessing...");
            try
            {
                await _deployManager.SynchronizeRemoteAsync();
                var executor = new RemoteCommandExecutor("Preprocessing", _channel, _outputWindow);
                var result = await executor.ExecuteWithResultAsync(command, options.RemoteOutputFile);

                if (!result.TryGetResult(out var execResult, out var error))
                    throw new System.Exception(error.Message);
                var (_, data) = execResult;

                File.WriteAllBytes(options.LocalOutputCopyPath, data);
                OpenFileInEditor(options.LocalOutputCopyPath, options.LineMarker);
            }
            finally
            {
                await ClearStatusBarAsync();
            }
        }
    }
}
