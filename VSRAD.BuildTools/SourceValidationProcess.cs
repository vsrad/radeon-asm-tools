using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace VSRAD.BuildTools
{
    public class SourceValidationProcess
    {
        public delegate void OnOutput(string output);

        private readonly StringBuilder _stderrBuffer = new StringBuilder();
        private readonly Process _process = new Process();

        public SourceValidationProcess(string exe, string args, OnOutput stdoutHandler, OnOutput stderrHandler,
            string env)
        {
            _process.StartInfo = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string variable in env.Split(';'))
            {
                if (variable == "")
                {
                    continue;
                }
                var v = variable.Split('=');
                _process.StartInfo.EnvironmentVariables[v[0]] = v[1];
            }

            _process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    stdoutHandler(e.Data);
                }
            };
            _process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _stderrBuffer.AppendLine(e.Data);
                    stderrHandler(e.Data);
                }
            };
        }

        public int WaitForExit()
        {
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            _process.WaitForExit(-1);

            return _process.ExitCode;
        }

        public string GetBufferedStderr()
        {
            Debug.Assert(_process.HasExited);
            return _stderrBuffer.ToString();
        }
    }
}
