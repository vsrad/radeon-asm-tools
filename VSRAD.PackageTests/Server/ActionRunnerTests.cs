using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class ActionRunnerTests
    {
        [Fact]
        public async Task SucessfulRunTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, SourcePath = "tweened.tvpp", TargetPath = Path.GetTempFileName() }
            };
            var localTempFile = Path.GetRandomFileName();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) }, (FetchMetadata command) =>
            {
                // init timestamp fetch
                Assert.Equal(new[] { "/home/mizu/machete", "tweened.tvpp" }, command.FilePath);
            });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "", Stderr = "" });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(101), Data = Encoding.UTF8.GetBytes("file-contents") });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);
            Assert.True(result.StepResults[0].Successful);
            Assert.Equal("", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (exit code 0)\r\n", result.StepResults[0].Log);
            Assert.True(result.StepResults[1].Successful);
            Assert.Equal("", result.StepResults[1].Warning);
            Assert.Equal("", result.StepResults[1].Log);
            Assert.Equal("file-contents", File.ReadAllText(((CopyFileStep)steps[1]).TargetPath));
            File.Delete(((CopyFileStep)steps[1]).TargetPath);
        }

        #region CopyFileStep
        [Fact]
        public async Task CopyRLRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));
            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, SourcePath = "/home/mizu/machete/key3_49" },
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" } // should not be run
            };

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }); // init timestamp fetch
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.FileNotFound });
            var result = await runner.RunAsync("HTMT", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("File is not found on the remote machine at /home/mizu/machete/key3_49", result.StepResults[0].Warning);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) }); // init timestamp fetch
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) });
            result = await runner.RunAsync("HTMT", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("File is not changed on the remote machine at /home/mizu/machete/key3_49", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyRLMissingParentDirectoryTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));

            var parentDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Assert.False(Directory.Exists(parentDir));

            var file = Path.Combine(parentDir, "local-copy");
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "raw3", TargetPath = file } };

            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);
            Assert.Equal("file-contents", File.ReadAllText(file));
            File.Delete(file);
        }

        [Fact]
        public async Task CopyRLLocalErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));

            var file = Path.GetTempFileName();
            File.SetAttributes(file, FileAttributes.ReadOnly);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "raw3", TargetPath = file } };
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });

            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access to path {file} on the local machine is denied", result.StepResults[0].Warning);

            file = @"C:\Users\mizu*~*\raw >_<";
            steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "raw3", TargetPath = file } };
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });

            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Local path contains illegal characters: \"{file}\"\r\nWorking directory: \"{Path.GetTempPath()}\"", result.StepResults[0].Warning);

            file = Path.Combine(Path.GetTempPath(), "raw*o*");
            file += "=>_<=";
            steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "raw3", TargetPath = file } };
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });

            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Local path contains illegal characters: \"{file}\"\r\nWorking directory: \"{Path.GetTempPath()}\"", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyLRRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = Path.GetTempFileName(), TargetPath = "raw3" } };

            channel.ThenRespond(new PutFileResponse { Status = PutFileStatus.PermissionDenied });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal("Access to path raw3 on the remote machine is denied", result.StepResults[0].Warning);

            channel.ThenRespond(new PutFileResponse { Status = PutFileStatus.OtherIOError });
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal("File raw3 could not be created on the remote machine", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyLRLocalErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));

            var localPath = @"C:\Non\Existent\Path\To\Users\mizu\raw3";
            Assert.False(File.Exists(localPath));
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = localPath, TargetPath = "" } };

            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal(@"File C:\Non\Existent\Path\To\Users\mizu\raw3 is not found on the local machine", result.StepResults[0].Warning);

            var lockedPath = Path.GetTempFileName();
            var acl = File.GetAccessControl(lockedPath);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.Read, AccessControlType.Deny));
            File.SetAccessControl(lockedPath, acl);

            steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = lockedPath, TargetPath = "" } };
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access to path {lockedPath} on the local machine is denied", result.StepResults[0].Warning);
            File.Delete(lockedPath);

            var illegalPath = @"C:\Users\mizu\raw *~* >_<";
            steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = illegalPath, TargetPath = "" } };
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Local path contains illegal characters: \"{illegalPath}\"\r\nWorking directory: \"{Path.GetTempPath()}\"", result.StepResults[0].Warning);
        }
        #endregion

        #region ExecuteStep
        [Fact]
        public async Task ExecuteRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "dvd-prepare" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = false, TargetPath = "/home/parker/audio/unchecked", SourcePath = "" }, // should not be run
            };
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/parker/audio"));

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch, Stdout = "", Stderr = "" });
            var result = await runner.RunAsync("UFOW", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("dvd-prepare process could not be started on the remote machine. Make sure the path to the executable is specified correctly.", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (could not launch)\r\n", result.StepResults[0].Log);

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.TimedOut, Stdout = "...\n", Stderr = "Could not prepare master DVD, deadline exceeded.\n\n" });
            result = await runner.RunAsync("UFOW", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Execution timeout is exceeded. dvd-prepare process on the remote machine is terminated.", result.StepResults[0].Warning);
            Assert.Equal("Captured stdout (timed out):\r\n...\r\nCaptured stderr (timed out):\r\nCould not prepare master DVD, deadline exceeded.\r\n", result.StepResults[0].Log);

            /* Non-zero exit code results in a failed run with an error */
            steps = new List<IActionStep> { new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "dvd-prepare" } };
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 1, Stdout = "", Stderr = "Looks like you fell asleep ¯\\_(ツ)_/¯\n\n" });
            result = await runner.RunAsync("UFOW", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("dvd-prepare process exited with a non-zero code (1). Check your application or debug script output in Output -> RAD Debug.", result.StepResults[0].Warning);
            Assert.Equal("Captured stderr (exit code 1):\r\nLooks like you fell asleep ¯\\_(ツ)_/¯\r\n", result.StepResults[0].Log);
        }

        [Fact]
        public async Task ExecuteLocalTestAsync()
        {
            var file = Path.GetTempFileName();

            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Local, Executable = "python.exe", Arguments = $"-c \"print('success', file=open(r'{file}', 'w'))\"" }
            };
            var runner = new ActionRunner(channel: null, serviceProvider: null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: ""));
            var result = await runner.RunAsync("", steps);
            Assert.True(result.Successful);
            Assert.Equal("", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (exit code 0)\r\n", result.StepResults[0].Log);

            var output = File.ReadAllText(file);
            File.Delete(file);
            Assert.Equal("success\r\n", output);
        }

        [Fact]
        public async Task ExecuteLocalWorkingDirectoryTestAsync()
        {
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Local, Executable = "python.exe", Arguments = $"-c \"import os; print(os.getcwd())\"" }
            };
            var env = new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "");
            var runner = new ActionRunner(channel: null, serviceProvider: null, env);
            var result = await runner.RunAsync("", steps);
            Assert.True(result.Successful);

            // When working directory is not specified, it defaults to ActionEnvironment.LocalWorkDir
            var expectedWorkDir = env.LocalWorkDir.TrimEnd('\\');
            Assert.Equal($"Captured stdout (exit code 0):\r\n{expectedWorkDir}\r\n", result.StepResults[0].Log);

            ((ExecuteStep)steps[0]).WorkingDirectory = Directory.GetCurrentDirectory();
            result = await runner.RunAsync("", steps);
            Assert.True(result.Successful);

            // When working directory is set, it should override ActionEnvironment.LocalWorkDir
            expectedWorkDir = Directory.GetCurrentDirectory();
            Assert.Equal($"Captured stdout (exit code 0):\r\n{expectedWorkDir}\r\n", result.StepResults[0].Log);
        }

        [Fact]
        public async Task ExecuteRemoteWorkingDirectoryTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep> { new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "exe" } };
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/action/env/remote/dir"));

            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted(), command =>
            {
                Assert.Equal("/action/env/remote/dir", command.WorkingDirectory);
            });
            await runner.RunAsync("", steps);
            Assert.True(channel.AllInteractionsHandled);

            ((ExecuteStep)steps[0]).WorkingDirectory = "/explicitly/set/remote/dir";
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted(), command =>
            {
                Assert.Equal("/explicitly/set/remote/dir", command.WorkingDirectory);
            });
            await runner.RunAsync("", steps);
            Assert.True(channel.AllInteractionsHandled);
        }
        #endregion

        [Fact]
        public async Task RunActionStepTestAsync()
        {
            var channel = new MockCommunicationChannel();

            var level3Steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "cleanup", Arguments = "--skip" },
            };
            var level2Steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" },
                new RunActionStep(level3Steps) { Name = "level3" }
            };
            var level1Steps = new List<IActionStep>
            {
                new RunActionStep(level2Steps) { Name = "level2" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, SourcePath = "/home/mizu/machete/tweened.tvpp", TargetPath = Path.GetTempFileName() }
            };
            // 1. Initial timestamp fetch
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) });
            // 2. Level 2 Execute
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "level2", Stderr = "" }, (Execute command) =>
            {
                Assert.Equal("autotween", command.Executable);
            });
            // 3. Level 3 Execute
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "level3", Stderr = "" }, (Execute command) =>
            {
                Assert.Equal("cleanup", command.Executable);
            });
            // 4. Level 1 Copy File
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(101), Data = Encoding.UTF8.GetBytes("file-contents") });
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));
            var result = await runner.RunAsync("HTMT", level1Steps);

            Assert.True(result.Successful);
            Assert.Equal("level2", result.StepResults[0].SubAction.ActionName);
            Assert.Null(result.StepResults[0].SubAction.StepResults[0].SubAction);
            Assert.Equal("Captured stdout (exit code 0):\r\nlevel2\r\n", result.StepResults[0].SubAction.StepResults[0].Log);
            Assert.Equal("level3", result.StepResults[0].SubAction.StepResults[1].SubAction.ActionName);
            Assert.Equal("Captured stdout (exit code 0):\r\nlevel3\r\n", result.StepResults[0].SubAction.StepResults[1].SubAction.StepResults[0].Log);
            Assert.Null(result.StepResults[1].SubAction);
        }

        #region ReadDebugDataStep
        [Fact]
        public async Task ReadDebugDataRemoteTestAsync()
        {
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "va11" },
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "output", CheckTimestamp = true },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "watches", CheckTimestamp = false },
                    statusFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "status", CheckTimestamp = false },
                    binaryOutput: true, outputOffset: 0)
            };

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/glitch/city"));

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata initTimestampFetch) =>
                Assert.Equal(new[] { "/glitch/city", "output" }, initTimestampFetch.FilePath));
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("jill\njulianne") }, (FetchResultRange watchesFetch) =>
                Assert.Equal(new[] { "/glitch/city", "watches" }, watchesFetch.FilePath));
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes(@"
grid size (8192, 0, 0)
group size (512, 0, 0)
wave size 32
comment 115200") }, (FetchResultRange statusFetch) =>
                Assert.Equal(new[] { "/glitch/city", "status" }, statusFetch.FilePath));
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now }, (FetchMetadata outputMetaFetch) =>
                Assert.Equal(new[] { "/glitch/city", "output" }, outputMetaFetch.FilePath));

            var result = await runner.RunAsync("Debug", steps);

            Assert.True(channel.AllInteractionsHandled);
            Assert.True(result.Successful);
            Assert.NotNull(result.BreakState);
            Assert.Collection(result.BreakState.Data.Watches,
                (first) => Assert.Equal("jill", first),
                (second) => Assert.Equal("julianne", second));
            Assert.NotNull(result.BreakState.DispatchParameters);
            Assert.Equal<uint>(8192 / 512, result.BreakState.DispatchParameters.DimX);
            Assert.Equal<uint>(512, result.BreakState.DispatchParameters.GroupSize);
            Assert.Equal<uint>(32, result.BreakState.DispatchParameters.WaveSize);
            Assert.False(result.BreakState.DispatchParameters.NDRange3D);
            Assert.Equal("115200", result.BreakState.DispatchParameters.StatusString);
        }

        [Fact]
        public async Task ReadDebugDataRemoteErrorTestAsync()
        {
            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "remote/output", CheckTimestamp = true },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "remote/watches", CheckTimestamp = true },
                    statusFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "remote/status", CheckTimestamp = true },
                    binaryOutput: true, outputOffset: 0)
            };

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/glitch/city"));

            /* File not found */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(i) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.FileNotFound }, (FetchResultRange w) => Assert.Equal("remote/watches", w.FilePath[1]));

            var result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Valid watches file (remote/watches) could not be found.", result.StepResults[0].Warning);

            /* File not changed */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(i) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(0) },
                (FetchResultRange w) => Assert.Equal("remote/watches", w.FilePath[1]));

            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Valid watches file (remote/watches) was not modified.", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task ReadDebugDataLocalTestAsync()
        {
            var outputFile = Path.GetTempFileName();
            var watchesFile = Path.GetTempFileName();
            var statusFile = Path.GetTempFileName();

            File.WriteAllText(watchesFile, "jill\r\njulianne");
            File.WriteAllText(statusFile, @"
grid size (64, 0, 0)
group size (64, 0, 0)
wave size 64");
            var output = new uint[192];
            for (int lane = 0; lane < 64; ++lane)
            {
                output[lane * 3 + 0] = 1;          // system = const
                output[lane * 3 + 1] = 777;        // first watch = const
                output[lane * 3 + 2] = (uint)lane; // second watch = lane
            }
            byte[] outputBytes = new byte[output.Length * sizeof(int)];
            Buffer.BlockCopy(output, 0, outputBytes, 0, outputBytes.Length);
            File.WriteAllBytes(outputFile, outputBytes);

            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = outputFile, CheckTimestamp = false },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = watchesFile, CheckTimestamp = false },
                    statusFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = statusFile, CheckTimestamp = false },
                    binaryOutput: true, outputOffset: 0)
            };
            var runner = new ActionRunner(null, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), ""));
            var result = await runner.RunAsync("Debug", steps);

            Assert.True(result.Successful);
            Assert.NotNull(result.BreakState);
            Assert.Collection(result.BreakState.Data.Watches,
                (first) => Assert.Equal("jill", first),
                (second) => Assert.Equal("julianne", second));
            Assert.Equal<uint>(1, result.BreakState.DispatchParameters.DimX);
            Assert.Equal<uint>(64, result.BreakState.DispatchParameters.GroupSize);
            Assert.Equal<uint>(64, result.BreakState.DispatchParameters.WaveSize);

            var secondWatch = result.BreakState.Data.GetWatch("julianne");
            for (int i = 0; i < 64; ++i)
                Assert.Equal(i, (int)secondWatch[i]);
        }

        [Fact]
        public async Task ReadDebugDataLocalErrorTestAsync()
        {
            var outputFile = Path.GetTempFileName();
            var fileName = Path.GetFileName(outputFile);

            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = fileName, CheckTimestamp = true },
                    watchesFile: new BuiltinActionFile(),
                    statusFile: new BuiltinActionFile(),
                    binaryOutput: true, outputOffset: 0)
            };

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: ""));

            /* File not changed (GetTempFileName creates an empty file) */

            var result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Output file ({fileName}) was not modified. Data may be stale.", result.StepResults[0].Warning);

            /* Access denied */

            var acl = File.GetAccessControl(outputFile);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.Read, AccessControlType.Deny));
            File.SetAccessControl(outputFile, acl);

            ((ReadDebugDataStep)steps[0]).OutputFile.CheckTimestamp = false;
            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Output file could not be opened. Access to path {fileName} on the local machine is denied", result.StepResults[0].Warning);

            /* File not found */

            File.Delete(outputFile);
            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Output file could not be opened. File {fileName} is not found on the local machine", result.StepResults[0].Warning);

            /* Invalid path */

            ((ReadDebugDataStep)steps[0]).OutputFile.Path += "<>";
            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Output file could not be opened. Local path contains illegal characters: \"{fileName}<>\"\r\nWorking directory: \"{Path.GetTempPath()}\"", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task ReadDebugDataLocalTextOutputTestAsync()
        {
            var outputFile = Path.GetTempFileName();
            File.WriteAllText(outputFile, @"SKIPPED LINE
0x6173616b
0x7573616d
0x69646f72
0x69333133
");

            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = outputFile, CheckTimestamp = false },
                    watchesFile: new BuiltinActionFile(), statusFile: new BuiltinActionFile(), binaryOutput: false, outputOffset: 1)
            };
            var runner = new ActionRunner(null, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), "", watches: new ReadOnlyCollection<string>(new[] { "const" })));
            var result = await runner.RunAsync("Debug", steps);

            Assert.True(result.Successful);
            var system = result.BreakState.Data.GetSystem();
            Assert.Equal(0x6173616b, (int)system[0]);
            Assert.Equal(0x69646f72, (int)system[1]);
            var constWatch = result.BreakState.Data.GetWatch("const");
            Assert.Equal(0x7573616d, (int)constWatch[0]);
            Assert.Equal(0x69333133, (int)constWatch[1]);
        }

        #endregion

        [Fact]
        public async Task VerifiesTimestampsTestAsync()
        {
            var channel = new MockCommunicationChannel();

            var readDebugData = new ReadDebugDataStep();
            readDebugData.OutputFile.Location = StepEnvironment.Remote;
            readDebugData.OutputFile.CheckTimestamp = true;
            readDebugData.OutputFile.Path = "/home/parker/audio/master";
            readDebugData.StatusFile.Location = StepEnvironment.Remote;
            readDebugData.StatusFile.CheckTimestamp = false;
            readDebugData.StatusFile.Path = "/home/parker/audio/copy";
            //readDebugData.WatchesFile.Location = StepEnvironment.Local;
            //readDebugData.WatchesFile.CheckTimestamp = true;
            //readDebugData.WatchesFile.Path = "non-existent-local-path";
            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, SourcePath = "/home/parker/audio/checked", TargetPath = Path.GetTempFileName() },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = false, SourcePath = "/home/parker/audio/unchecked", TargetPath = Path.GetTempFileName() },
                readDebugData
            };

            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/parker"));

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(100) }, (FetchMetadata command) =>
                Assert.Equal(new[] { "/home/parker", "/home/parker/audio/checked" }, command.FilePath));
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata command) =>
                Assert.Equal(new[] { "/home/parker", "/home/parker/audio/master" }, command.FilePath));

            channel.ThenRespond(new ResultRangeFetched { Data = Encoding.UTF8.GetBytes("TestCopyStepChecked") },
                (FetchResultRange command) => Assert.Equal(new[] { "/home/parker", "/home/parker/audio/checked" }, command.FilePath));
            channel.ThenRespond(new ResultRangeFetched { Data = Encoding.UTF8.GetBytes("TestCopyStepUnchecked") },
                (FetchResultRange command) => Assert.Equal(new[] { "/home/parker", "/home/parker/audio/unchecked" }, command.FilePath));

            // ReadDebugDataStep
            channel.ThenRespond(new ResultRangeFetched());
            channel.ThenRespond(new MetadataFetched());

            await runner.RunAsync("UFOW", steps);
            Assert.True(channel.AllInteractionsHandled);

            Assert.Equal(DateTime.FromFileTime(100), runner.GetInitialFileTimestamp("/home/parker/audio/checked"));
            Assert.Equal(default, runner.GetInitialFileTimestamp("/home/parker/audio/master"));

            Assert.Equal("TestCopyStepChecked", File.ReadAllText(((CopyFileStep)steps[0]).TargetPath));
            File.Delete(((CopyFileStep)steps[0]).TargetPath);
            Assert.Equal("TestCopyStepUnchecked", File.ReadAllText(((CopyFileStep)steps[1]).TargetPath));
            File.Delete(((CopyFileStep)steps[1]).TargetPath);
        }
    }
}
