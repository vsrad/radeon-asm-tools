using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.ProfileCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class ProfileCommand : BaseRemoteCommand
    {
        private readonly IProject _project;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly IOutputWindowManager _outputWindow;
        private readonly ICommunicationChannel _channel;
        private readonly IErrorListManager _errorListManager;

        [ImportingConstructor]
        public ProfileCommand(
            IProject project,
            IFileSynchronizationManager deployManager,
            IOutputWindowManager outputWindow,
            ICommunicationChannel channel,
            SVsServiceProvider serviceProvider,
            IErrorListManager errorListManager) : base(Constants.ProfileCommandId, serviceProvider)
        {
            _project = project;
            _deployManager = deployManager;
            _outputWindow = outputWindow;
            _channel = channel;
            _errorListManager = errorListManager;
        }

        public override async Task RunAsync(long commandId)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.Profiler.EvaluateAsync(evaluator);
            var command = new Execute { Executable = options.Executable, Arguments = options.Arguments, WorkingDirectory = options.WorkingDirectory, RunAsAdministrator = options.RunAsAdmin };

            await SetStatusBarTextAsync("RAD Debug: Profiling...");
            try
            {
                await _deployManager.SynchronizeRemoteAsync();
                var executor = new RemoteCommandExecutor("Profile", _channel, _outputWindow, _errorListManager);

                var result = await executor.ExecuteWithResultAsync(command, options.RemoteOutputFile);

                if (!result.TryGetResult(out var execResult, out var error))
                    throw new System.Exception(error.Message);
                var (_, data) = execResult;

                File.WriteAllBytes(options.LocalOutputCopyPath, data);

                if (!string.IsNullOrWhiteSpace(options.ViewerExecutable))
                    Process.Start(options.ViewerExecutable, options.ViewerArguments);
            }
            finally
            {
                await ClearStatusBarAsync();
            }
        }
    }
}
