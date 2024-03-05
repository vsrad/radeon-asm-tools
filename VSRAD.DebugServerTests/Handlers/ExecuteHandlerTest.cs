using System.Collections.Generic;
using System.IO;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class ExecuteHandlerTest
    {
        [Fact]
        public async void SetsEnvironmentVariablesTestAsync()
        {
            var pyTest = "import os;" +
                "print(os.environ['TEST_VAR_1']);" +
                "print(os.environ['TEST_VAR_2']);";

            var response = await Helper.DispatchCommandAsync<Execute, ExecutionCompleted>(
                new Execute
                {
                    Executable = "python.exe",
                    Arguments = $"-c \"{pyTest}\"",
                    EnvironmentVariables = new Dictionary<string, string>() { { "TEST_VAR_1", "12345" }, { "TEST_VAR_2", "env test"} }
                });
            Assert.Equal(ExecutionStatus.Completed, response.Status);
            Assert.Equal(0, response.ExitCode);
            Assert.Equal("12345\r\nenv test\r\n", response.Stdout);
        }

        [Fact]
        public async void RunsExternalCommandsAsync()
        {
            var tmpFile = Path.GetTempFileName();
            var tmpDirectory = Path.GetDirectoryName(tmpFile);
            var tmpFileRelative = Path.GetFileName(tmpFile);

            var pyTest = "import sys;" +
                "sys.stderr.write('lain not found');" +
                "print('now decide');" +
                "print('do you wish to proceed to live? [Yn]');" +
                $"print('command ran successfully',  file=open('{tmpFileRelative}', 'w'))";

            var response = await Helper.DispatchCommandAsync<Execute, ExecutionCompleted>(
                new Execute
                {
                    WorkingDirectory = tmpDirectory,
                    Executable = "python.exe",
                    Arguments = $"-c \"{pyTest}\""
                });
            Assert.Equal(ExecutionStatus.Completed, response.Status);
            Assert.Equal(0, response.ExitCode);
            Assert.Equal("now decide\r\ndo you wish to proceed to live? [Yn]\r\n", response.Stdout);
            Assert.Equal("lain not found\r\n", response.Stderr);

            var tmpContents = File.ReadAllText(tmpFile);
            Assert.Equal("command ran successfully\r\n", tmpContents);
        }

        [Fact]
        public async void TimeoutTestAsync()
        {
            var response = await Helper.DispatchCommandAsync<Execute, ExecutionCompleted>(
                new Execute
                {
                    Executable = "python.exe",
                    Arguments = $"-c \"import time\ntime.sleep(2)\"",
                    RunAsAdministrator = false,
                    ExecutionTimeoutSecs = 1
                });
            Assert.Equal(ExecutionStatus.TimedOut, response.Status);

            response = await Helper.DispatchCommandAsync<Execute, ExecutionCompleted>(
                new Execute
                {
                    Executable = "python.exe",
                    Arguments = $"-c \"import time\ntime.sleep(1)\"",
                    RunAsAdministrator = false,
                    ExecutionTimeoutSecs = 3
                });
            Assert.Equal(ExecutionStatus.Completed, response.Status);
        }

        [Fact]
        public async void NonZeroExitCodeTestAsync()
        {
            var response = await Helper.DispatchCommandAsync<Execute, ExecutionCompleted>(
                new Execute
                {
                    Executable = "python.exe",
                    Arguments = $"-c \"some non-valid python code here\"",
                    RunAsAdministrator = false,
                    ExecutionTimeoutSecs = 0
                });
            Assert.Equal(ExecutionStatus.Completed, response.Status);
            Assert.Equal(1, response.ExitCode);
        }

        [Fact]
        public async void MissingExecutableTestAsync()
        {
            var response = await Helper.DispatchCommandAsync<Execute, ExecutionCompleted>(
                new Execute
                {
                    Executable = "eva-00.exe",
                    Arguments = "plug eject"
                });
            Assert.Equal(ExecutionStatus.CouldNotLaunch, response.Status);

            response = await Helper.DispatchCommandAsync<Execute, ExecutionCompleted>(
                new Execute
                {
                    Executable = ""
                });
            Assert.Equal(ExecutionStatus.CouldNotLaunch, response.Status);
            response.ToString(); // must not throw
        }
    }
}
