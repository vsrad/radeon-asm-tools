using Microsoft.VisualStudio.ProjectSystem;
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
    public interface IBuildToolsServer
    {
        Task RunAsync(IProject project, ICommunicationChannel channel, IOutputWindowManager outputWindow);
    }

    [Export(typeof(IBuildToolsServer))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class BuildToolsServer : IBuildToolsServer
    {
        public string PipeName { get; } = "1.buildpipe";

        [ImportingConstructor]
        public BuildToolsServer() { }

        public async Task RunAsync(IProject project, ICommunicationChannel channel, IOutputWindowManager outputWindow)
        {
            using (var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            {
                await server.WaitForConnectionAsync();

                var executor = new RemoteCommandExecutor("Build", channel, outputWindow);
                var buildResult = await BuildAsync(project, executor);

                byte[] message;
                if (buildResult.TryGetResult(out var result, out var error))
                    message = result.ToArray();
                else
                    message = new IPCBuildResult { ExitCode = 0, Stdout = "", Stderr = "" }.ToArray();

                await server.WriteAsync(message, 0, message.Length);
            }
        }

        private async Task<Result<IPCBuildResult>> BuildAsync(IProject project, RemoteCommandExecutor executor)
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
