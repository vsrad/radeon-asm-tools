using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class ExecuteHandler : IHandler
    {
        private readonly ClientLogger _log;
        private readonly ProcessStartInfo _processStartInfo;
        private readonly int _timeout;
        private bool _stoppedByTimeout = false;

        public ExecuteHandler(IPC.Commands.Execute command, ClientLogger log)
        {
            _log = log;
            _processStartInfo = new ProcessStartInfo(command.Executable, command.Arguments)
            {
                WorkingDirectory = command.WorkingDirectory
            };
            if (command.RunAsAdministrator)
            {
                _processStartInfo.UseShellExecute = true;
                _processStartInfo.Verb = "runas";
            }
            else
            {
                _processStartInfo.UseShellExecute = false;
                _processStartInfo.RedirectStandardOutput = true;
                _processStartInfo.RedirectStandardError = true;
            }
            _timeout = command.ExecutionTimeoutSecs;
        }

        public async Task<IResponse> RunAsync()
        {
            var process = new Process()
            {
                StartInfo = _processStartInfo,
                EnableRaisingEvents = true
            };

            var processExitedTcs = new TaskCompletionSource<bool>();
            process.Exited += (sender, args) => processExitedTcs.SetResult(true);

            var (stdoutTask, stderrTask) = InitializeOutputCapture(process);

            try
            {
                process.Start();
                _log.ExecutionStarted();
                if (_processStartInfo.RedirectStandardOutput)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                if (_timeout != 0)
                {
                    SetProcessTimeout(process);
                }
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

            return new ExecutionCompleted { Status = status, ExitCode = process.ExitCode, Stdout = stdout, Stderr = stderr };
        }

        private (Task<string> stdout, Task<string> stderr) InitializeOutputCapture(Process process)
        {
            if (!_processStartInfo.RedirectStandardOutput)
                return (Task.FromResult(""), Task.FromResult(""));

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            var stdoutTcs = new TaskCompletionSource<string>();
            var stderrTcs = new TaskCompletionSource<string>();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data == null)
                {
                    // According to the docs, "when a redirected stream is closed, a null line is sent to the event handler"
                    stdoutTcs.SetResult(stdout.ToString());
                }
                else
                {
                    stdout.AppendLine(args.Data);
                    _log.StdoutReceived(args.Data);
                }
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data == null)
                {
                    stderrTcs.SetResult(stderr.ToString());
                }
                else
                {
                    stderr.AppendLine(args.Data);
                    _log.StderrReceived(args.Data);
                }
            };

            return (stdoutTcs.Task, stderrTcs.Task);
        }

        private void SetProcessTimeout(Process process)
        {
            var _processTimeout = new System.Timers.Timer()
            {
                AutoReset = false,
                Interval = _timeout * 1000.0 // seconds -> milliseconds
            };
            _processTimeout.Elapsed += (sender, e) =>
            {
                try
                {
                    process.Kill();
                    _stoppedByTimeout = true;
                }
                catch (InvalidOperationException) { /* Already stopped */ }
            };
            _processTimeout.Start();
        }
    }
}
