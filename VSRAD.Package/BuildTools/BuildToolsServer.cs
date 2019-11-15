using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.BuildTools;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.BuildTools
{
    [Export]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class BuildToolsServer
    {
        public const string ErrorPreprocessorFileNotCreated = "Preprocessor output file is missing.";
        public const string ErrorPreprocessorFileUnchanged = "Preprocessor output file is unchanged after running the command.";

        public string PipeName => IPCBuildResult.GetIPCPipeName(_project.RootPath);

        private readonly ICommunicationChannel _channel;
        private readonly IOutputWindowManager _outputWindow;
        private readonly IProjectSourceManager _sourceManager;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly CancellationTokenSource _serverLoopCts = new CancellationTokenSource();

        private IProject _project;

        [ImportingConstructor]
        public BuildToolsServer(
            ICommunicationChannel channel,
            IOutputWindowManager outputWindow,
            IProjectSourceManager sourceManager,
            IFileSynchronizationManager deployManager)
        {
            _channel = channel;
            _outputWindow = outputWindow;
            _sourceManager = sourceManager;
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

                    byte[] message;
                    try
                    {
                        var buildResult = await BuildAsync().ConfigureAwait(false);
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

        private async Task<Result<IPCBuildResult>> BuildAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.Build.EvaluateAsync(evaluator);
            var projectSources = await _sourceManager.ListProjectFilesAsync();
            var executor = new RemoteCommandExecutor("Build", _channel, _outputWindow);

            if (string.IsNullOrEmpty(options.Executable))
                return new IPCBuildResult { ServerError = IPCBuildResult.ServerErrorBuildSkipped };

            await _deployManager.SynchronizeRemoteAsync().ConfigureAwait(false);

            var command = new Execute
            {
                Executable = options.Executable,
                Arguments = options.Arguments,
                WorkingDirectory = options.WorkingDirectory
            };

            ExecutionCompleted result;
            string preprocessed = "";
            if (string.IsNullOrEmpty(options.PreprocessedSource))
            {
                var response = await executor.ExecuteAsync(command, checkExitCode: false);
                if (!response.TryGetResult(out result, out var error))
                    return error;
            }
            else
            {
                var response = await executor.ExecuteWithResultAsync(command, options.PreprocessedSourceFile, checkExitCode: false);
                if (!response.TryGetResult(out var resultData, out var error))
                    switch (error.Message)
                    {
                        case RemoteCommandExecutor.ErrorFileNotCreated: return new Error(ErrorPreprocessorFileNotCreated);
                        case RemoteCommandExecutor.ErrorFileUnchanged: return new Error(ErrorPreprocessorFileUnchanged);
                        default: return error;
                    }

                result = resultData.Item1;
                preprocessed = Encoding.UTF8.GetString(resultData.Item2);
            }

            return new IPCBuildResult
            {
                ExitCode = result.ExitCode,
                Stdout = result.Stdout,
                Stderr = result.Stderr,
                PreprocessedSource = preprocessed,
                ProjectSourcePaths = projectSources.ToArray()
            };
        }
    }
}
