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
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    internal sealed class PreprocessCommand : BaseRemoteCommand
    {
        private readonly IProject _project;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly RemoteCommandExecutor _executor;

        [ImportingConstructor]
        public PreprocessCommand(
            IProject project,
            IFileSynchronizationManager deployManager,
            ICommunicationChannel channel,
            IOutputWindowManager outputWindow,
            IErrorListManager errorList,
            SVsServiceProvider serviceProvider)
            : base(Constants.PreprocessCommandSet, Constants.PreprocessCommandId, serviceProvider)
        {
            _project = project;
            _deployManager = deployManager;
            _executor = new RemoteCommandExecutor("Preprocessor", channel, outputWindow, errorList);
        }

        public override async Task RunAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await Options.PreprocessorProfileOptions.EvaluateAsync(evaluator);
            var command = new Execute { Executable = options.Executable, Arguments = options.Arguments, WorkingDirectory = options.WorkingDirectory };

            if (string.IsNullOrEmpty(options.LocalOutputCopyPath))
                throw new System.Exception("Preprocessor execution failed: local output path is not set. Configure it in your current profile settings, which can be found in Tools -> RAD Debug -> Options.");

            await SetStatusBarTextAsync("RAD Preprocessor is running...");
            try
            {
                await _deployManager.SynchronizeRemoteAsync();

                var result = await _executor.ExecuteWithResultAsync(command, options.RemoteOutputFile);
                if (!result.TryGetResult(out var execResult, out var error))
                    throw new System.Exception(error.Title + ": " + error.Message);
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
