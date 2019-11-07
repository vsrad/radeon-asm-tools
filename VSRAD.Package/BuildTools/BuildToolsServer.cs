using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using System.Diagnostics;

namespace VSRAD.Package.BuildTools
{
    public interface IBuildToolsServer
    {
        Task RunAsync();
    }

    [Export(typeof(IBuildToolsServer))]
    [AppliesTo(Constants.ProjectCapability)]
    class BuildToolsServer : IBuildToolsServer
    {
        private string _pipeName;

        [ImportingConstructor]
        public BuildToolsServer()
        {
            _pipeName = "1.buildpipe";
        }

        public async Task RunAsync()
        {
            using (var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            {
                await server.WaitForConnectionAsync();

                Build();
            }
        }

        private void Build()
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c ..\\release.bat");
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            var process = Process.Start(processInfo);
            process.WaitForExit();

            var exitcode = process.ExitCode;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            process.Close();
        }
        
    }
}
