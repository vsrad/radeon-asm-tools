using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using System.IO.Pipes;
using System.Threading;
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
        private readonly IFileSynchronizationManager _deployManager;
        private readonly CancellationTokenSource _serverLoopCts = new CancellationTokenSource();

        private IProject _project;

        [ImportingConstructor]
        public BuildToolsServer(ICommunicationChannel channel, IOutputWindowManager outputWindow, IFileSynchronizationManager deployManager)
        {
            _channel = channel;
            _outputWindow = outputWindow;
            _deployManager = deployManager;
        }

        public void SetProjectOnLoad(IProject project)
        {
            _project = project;
            VSPackage.TaskFactory.RunAsync(RunServerLoopAsync);
        }

        public void OnProjectUnloading()
        {
            _serverLoopCts.Cancel();
        }

        public async Task RunServerLoopAsync()
        {
            while (!_serverLoopCts.Token.IsCancellationRequested)
                using (var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                {
                    await server.WaitForConnectionAsync(_serverLoopCts.Token).ConfigureAwait(false);

                    var executor = new RemoteCommandExecutor("Build", _channel, _outputWindow);

                    byte[] message;
                    try
                    {
                        var buildResult = await BuildAsync(_project, _deployManager, executor).ConfigureAwait(false);
                        if (buildResult.TryGetResult(out var result, out var error))
                            message = result.ToArray();
                        else
                            message = new IPCBuildResult { ServerError = error.Message }.ToArray();
                    }
                    catch (Exception e)
                    {
                        message = new IPCBuildResult { ServerError = e.Message }.ToArray();
                    }

                    await server.WriteAsync(message, 0, message.Length);
                }
        }

        private static async Task<Result<IPCBuildResult>> BuildAsync(IProject project, IFileSynchronizationManager deployManager, RemoteCommandExecutor executor)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var evaluator = await project.GetMacroEvaluatorAsync(default);
            var options = await project.Options.Profile.Build.EvaluateAsync(evaluator);

            if (string.IsNullOrEmpty(options.Executable))
                return new IPCBuildResult { ServerError = IPCBuildResult.ServerErrorBuildSkipped };

            await deployManager.SynchronizeRemoteAsync().ConfigureAwait(false);

            var command = new Execute
            {
                Executable = options.Executable,
                Arguments = options.Arguments,
                WorkingDirectory = options.WorkingDirectory
            };
            var response = await executor.ExecuteAsync(command, checkExitCode: false);
            if (!response.TryGetResult(out var result, out var error))
                return error;

            return new IPCBuildResult { ExitCode = result.ExitCode, Stdout = result.Stdout, Stderr = result.Stderr };
        }
    }
}
