using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.SharedUtils
{
    public sealed class ObservableProcess
    {
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
            foreach (var env in command.EnvironmentVariables)
            {
                _process.StartInfo.EnvironmentVariables.Add(env.Key, env.Value);
            }
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
            _waitForCompletion = command.WaitForCompletion;
            _timeout = command.ExecutionTimeoutSecs;
        }

        public async Task<ExecutionCompleted> StartAndObserveAsync(CancellationToken cancellationToken = default)
        {
            if (!_waitForCompletion)
                return RunWithoutAwaitingCompletion();

            var processExitedTcs = new TaskCompletionSource<bool>();
            _process.Exited += (sender, args) => processExitedTcs.TrySetResult(true);

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
            catch (Win32Exception)
            {
                return new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch };
            }
            catch (InvalidOperationException) // missing executable name
            {
                return new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch };
            }

            var processTimeoutTcs = new TaskCompletionSource<bool>();
            if (_timeout != 0)
            {
                var timeoutTimer = new System.Timers.Timer { AutoReset = false, Interval = _timeout * 1000.0 /* ms */ };
                timeoutTimer.Elapsed += (sender, e) => processTimeoutTcs.TrySetResult(true);
                timeoutTimer.Start();
            }

            Task<bool> completedTask;
            using (cancellationToken.Register(() => processExitedTcs.TrySetCanceled()))
                completedTask = await Task.WhenAny(processExitedTcs.Task, processTimeoutTcs.Task);

            if (completedTask.IsCanceled || completedTask == processTimeoutTcs.Task)
            {
                await Task.Run(() => TerminateProcessTree(_process)); // This might take a few seconds, use Task.Run to avoid blocking the thread
                if (completedTask.IsCanceled)
                    throw new OperationCanceledException(cancellationToken);
            }

            var status = completedTask == processTimeoutTcs.Task ? ExecutionStatus.TimedOut : ExecutionStatus.Completed;

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new ExecutionCompleted { Status = status, ExitCode = _process.ExitCode, Stdout = stdout, Stderr = stderr, ExecutionTime = stopWatch.ElapsedMilliseconds };
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

        private static void TerminateProcessTree(Process parent)
        {
#if NETCOREAPP
            parent.Kill(entireProcessTree: true);
#else
            var searcher = new System.Management.ManagementObjectSearcher("SELECT ProcessID FROM Win32_Process WHERE ParentProcessID = " + parent.Id);
            var processCollection = searcher.Get();
            foreach (var p in processCollection)
            {
                try
                {
                    var childPid = Convert.ToInt32(p["ProcessID"]);
                    var child = Process.GetProcessById(childPid);
                    if (parent.StartTime < child.StartTime) // PIDs may be reused
                        TerminateProcessTree(child);
                }
                catch (ArgumentException) { /* already exited */ }
            }
            try
            {
                parent.Kill();
            }
            catch (Win32Exception) { /* cannot terminate */ }
            catch (InvalidOperationException) { /* cannot terminate */ }
#endif
        }
    }
}
