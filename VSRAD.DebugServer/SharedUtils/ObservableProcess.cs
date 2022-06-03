using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.SharedUtils
{
    public sealed class ObservableProcess
    {
        public delegate Task<bool> ConfirmTerminationOnTimeout(IList<ProcessTreeItem> processTree);

        public event EventHandler ExecutionStarted;
        public event EventHandler<string> StdoutRead;
        public event EventHandler<string> StderrRead;

        private readonly Process _process;
        private readonly bool _waitForCompletion;
        private readonly int _timeout;

        public ObservableProcess(IPC.Commands.Execute command)
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo(command.Executable, command.Arguments)
                {
                    WorkingDirectory = command.WorkingDirectory,
                    // window creation is necessary for run as admin
                    // because we can't capture stdout/stderr, so disable window
                    // creation only for non-administrator commands
                    CreateNoWindow = !command.RunAsAdministrator
                },
                EnableRaisingEvents = true
            };
            if (command.RunAsAdministrator)
            {
                _process.StartInfo.UseShellExecute = true;
                _process.StartInfo.Verb = "runas";
            }
            else
            {
                _process.StartInfo.UseShellExecute = false;
                _process.StartInfo.RedirectStandardOutput = true;
                _process.StartInfo.RedirectStandardError = true;
            }
            foreach (KeyValuePair<string, string> envVar in command.EnvironmentVariables)
                _process.StartInfo.EnvironmentVariables.Add(envVar.Key, envVar.Value);

            _waitForCompletion = command.WaitForCompletion;
            _timeout = command.ExecutionTimeoutSecs;
        }

        public async Task<IResponse> StartAndObserveAsync(ConfirmTerminationOnTimeout shouldTerminateOnTimeout, CancellationToken cancellationToken)
        {
            if (!_waitForCompletion)
                return RunWithoutAwaitingCompletion();

            var processExitedTcs = new TaskCompletionSource<bool>();
            _process.Exited += (sender, args) => processExitedTcs.TrySetResult(true);
            cancellationToken.Register(() => processExitedTcs.TrySetCanceled());

            var (stdoutTask, stderrTask) = InitializeOutputCapture();

            var stopWatch = Stopwatch.StartNew();
            try
            {
                _process.Start();
                ExecutionStarted?.Invoke(this, new EventArgs());
                if (_process.StartInfo.RedirectStandardOutput)
                {
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                }
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                return new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch, Stderr = e.Message };
            }
            catch (InvalidOperationException e)
            {
                return new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch, Stderr = e.Message };
            }

            var processTimeoutTcs = new TaskCompletionSource<bool>();
            if (_timeout != 0)
            {
                var timeoutTimer = new System.Timers.Timer { AutoReset = false, Interval = _timeout * 1000.0 /* ms */ };
                timeoutTimer.Elapsed += (sender, e) => processTimeoutTcs.SetResult(true);
                timeoutTimer.Start();
            }

            var completedTask = await Task.WhenAny(processExitedTcs.Task, processTimeoutTcs.Task);
            if (completedTask.IsCanceled || completedTask == processTimeoutTcs.Task)
            {
                // GetProcessTree call can take several seconds, it should not block the UI thread
                var processTree = await Task.Run(() => _process.GetProcessTree());
                if (completedTask.IsCanceled)
                {
                    ProcessUtils.TerminateProcessTree(processTree);
                    throw new OperationCanceledException(cancellationToken);
                }
                // The tree may be empty if the process has just exited
                if (processTree.Count > 0 && await shouldTerminateOnTimeout(processTree))
                {
                    var terminatedProcesses = ProcessUtils.TerminateProcessTree(processTree);
                    return new ExecutionTerminatedResponse { TerminatedProcessTree = terminatedProcesses.ToArray() };
                }
                // If the process is still running and should not be terminated, wait for it to exit
                await processExitedTcs.Task;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new ExecutionCompleted
            {
                Status = ExecutionStatus.Completed,
                ExitCode = _process.ExitCode,
                Stdout = stdout,
                Stderr = stderr,
                ExecutionTime = stopWatch.ElapsedMilliseconds
            };
        }

        private ExecutionCompleted RunWithoutAwaitingCompletion()
        {
            try
            {
                _process.Start();
                ExecutionStarted?.Invoke(this, new EventArgs());
                return new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 };
            }
            catch
            {
                return new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch };
            }
        }

        private (Task<string> stdout, Task<string> stderr) InitializeOutputCapture()
        {
            if (!_process.StartInfo.RedirectStandardOutput)
                return (Task.FromResult(""), Task.FromResult(""));

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            var stdoutTcs = new TaskCompletionSource<string>();
            var stderrTcs = new TaskCompletionSource<string>();

            _process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data == null)
                {
                    // According to the docs, "when a redirected stream is closed, a null line is sent to the event handler"
                    stdoutTcs.SetResult(stdout.ToString());
                }
                else
                {
                    stdout.AppendLine(args.Data);
                    StdoutRead?.Invoke(this, args.Data);
                }
            };
            _process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data == null)
                {
                    stderrTcs.SetResult(stderr.ToString());
                }
                else
                {
                    stderr.AppendLine(args.Data);
                    StderrRead?.Invoke(this, args.Data);
                }
            };

            return (stdoutTcs.Task, stderrTcs.Task);
        }
    }
}
