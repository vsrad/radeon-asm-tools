using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Threading;
using System.ComponentModel.Composition;
using System.IO.Pipes;
using System.Threading.Tasks;
using VSRAD.BuildTools;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.BuildTools
{
    [Export]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class BuildToolsServer
    {
        public string PipeName => IPCBuildResult.GetIPCPipeName(_project.RootPath);

        private readonly ICommunicationChannel _channel;
        private readonly IOutputWindowManager _outputWindow;

        private IProject _project;

        [ImportingConstructor]
        public BuildToolsServer(ICommunicationChannel channel, IOutputWindowManager outputWindow)
        {
            _channel = channel;
            _outputWindow = outputWindow;
        }

        public void SetProjectOnLoad(IProject project)
        {
            _project = project;
            VSPackage.TaskFactory.RunAsync(RunServerAsync, JoinableTaskCreationOptions.LongRunning);
        }

        public async Task RunServerAsync()
        {
            using (var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            {
                await server.WaitForConnectionAsync();

                var executor = new RemoteCommandExecutor("Build", _channel, _outputWindow);
                var buildResult = await BuildAsync(_project, executor);

                byte[] message;
                if (buildResult.TryGetResult(out var result, out var error))
                    message = result.ToArray();
                else
                    message = new IPCBuildResult { ExitCode = 0, Stdout = "", Stderr = "" }.ToArray();

                await server.WriteAsync(message, 0, message.Length);
            }
        }

        private static async Task<Result<IPCBuildResult>> BuildAsync(IProject project, RemoteCommandExecutor executor)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var evaluator = await project.GetMacroEvaluatorAsync(default);
            var options = await project.Options.Profile.Build.EvaluateAsync(evaluator); // TODO: Build options

            var response = await executor.ExecuteAsync(new Execute
            {
                Executable = options.Executable,
                Arguments = options.Arguments,
                WorkingDirectory = options.WorkingDirectory
            });
            if (!response.TryGetResult(out var result, out var error))
                return error;

            return new IPCBuildResult { ExitCode = result.ExitCode, Stdout = result.Stdout, Stderr = result.Stderr };
        }
    }
}
