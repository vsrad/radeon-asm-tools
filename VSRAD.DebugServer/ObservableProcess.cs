using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer
{
    public sealed class ObservableProcess
    {
        public event EventHandler ExecutionStarted;
        public event EventHandler<string> StdoutRead;
        public event EventHandler<string> StderrRead;

        private readonly Process _process;
        private readonly bool _waitForCompletion;
        private readonly int _timeout;
        private bool _stoppedByTimeout = false;

        public ObservableProcess(IPC.Commands.Execute command)
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo(command.Executable, command.Arguments)
                {
                    WorkingDirectory = command.WorkingDirectory
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
            _waitForCompletion = command.WaitForCompletion;
            _timeout = command.ExecutionTimeoutSecs;
        }

        public async Task<ExecutionCompleted> StartAndObserveAsync()
        {
            if (!_waitForCompletion)
                return RunWithoutAwaitingCompletion();

            var processExitedTcs = new TaskCompletionSource<bool>();
            _process.Exited += (sender, args) => processExitedTcs.SetResult(true);

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
                SetProcessTimeout();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch };
            }
            catch (InvalidOperationException) // missing executable name
            {
                return new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch };
            }

            await processExitedTcs.Task;

            var status = _stoppedByTimeout ? ExecutionStatus.TimedOut : ExecutionStatus.Completed;

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

        private void SetProcessTimeout()
        {
            if (_timeout == 0)
                return;

            var timeoutTimer = new System.Timers.Timer()
            {
                AutoReset = false,
                Interval = _timeout * 1000.0 // seconds -> milliseconds
            };
            timeoutTimer.Elapsed += (sender, e) =>
            {
                try
                {
                    _process.Kill();
                    _stoppedByTimeout = true;
                }
                catch (InvalidOperationException) { /* Already stopped */ }
            };
            timeoutTimer.Start();
        }
    }
}
