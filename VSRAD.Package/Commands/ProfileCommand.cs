using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    internal sealed class ProfileCommand : BaseRemoteCommand
    {
        private readonly IProject _project;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly RemoteCommandExecutor _executor;

        [ImportingConstructor]
        public ProfileCommand(
            IProject project,
            IFileSynchronizationManager deployManager,
            ICommunicationChannel channel,
            IOutputWindowManager outputWindow,
            IErrorListManager errorList,
            SVsServiceProvider serviceProvider) : base(Constants.ProfileCommandSet, Constants.ProfileCommandId, serviceProvider)
        {
            _project = project;
            _deployManager = deployManager;
            _executor = new RemoteCommandExecutor("Profiler", channel, outputWindow, errorList);
        }

        public override async Task RunAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.Profiler.EvaluateAsync(evaluator);
            var command = new Execute { Executable = options.Executable, Arguments = options.Arguments, WorkingDirectory = options.WorkingDirectory, RunAsAdministrator = options.RunAsAdmin };

            await SetStatusBarTextAsync("RAD Debug: Profiling...");
            try
            {
                await _deployManager.SynchronizeRemoteAsync();

                var result = await _executor.ExecuteWithResultAsync(command, options.RemoteOutputFile);
                if (!result.TryGetResult(out var execResult, out var error))
                    throw new System.Exception(error.Title + ": " + error.Message);
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
