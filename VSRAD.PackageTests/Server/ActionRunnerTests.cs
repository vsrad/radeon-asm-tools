using Moq;
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
using VSRAD.DebugServer.SharedUtils;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class ActionRunnerTests
    {
        IActionRunController MockController() => new Mock<IActionRunController>().Object;

        [Fact]
        public async Task SucessfulRunTestAsync()
        {
            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, FailIfNotModified = true, SourcePath = "/home/mizu/machete/tweened.tvpp", TargetPath = Path.GetTempFileName() }
            };
            var localTempFile = Path.GetRandomFileName();
            var runner = new ActionRunner(channel, MockController(), null);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) }, (FetchMetadata command) =>
            {
                // init timestamp fetch
                Assert.Equal(new[] { "/home/mizu/machete/tweened.tvpp" }, command.FilePath);
            });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "", Stderr = "" });
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", 1, DateTime.FromBinary(101)) } });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });
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
        public async Task CopyFileRLRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);
            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, FailIfNotModified = true, SourcePath = "/home/mizu/machete/key3_49", TargetPath = Path.GetRandomFileName() },
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" } // should not be run
            };

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }); // init timestamp fetch
            channel.ThenRespond(new ListFilesResponse { Files = Array.Empty<FileMetadata>() });
            var result = await runner.RunAsync("HTMT", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Path \"/home/mizu/machete/key3_49\" does not exist", result.StepResults[0].Warning);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) }); // init timestamp fetch
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", 1, DateTime.FromBinary(100)) } });
            result = await runner.RunAsync("HTMT", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("File was not changed after executing the previous steps. Disable Check Timestamp in step options to skip the modification date check.", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyFileRLMissingParentDirectoryTestAsync()
        {
            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);

            var parentDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Assert.False(Directory.Exists(parentDir));

            var file = Path.Combine(parentDir, "local-copy");
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "/home/mizu/machete/raw3", TargetPath = file } };

            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", 1, default) } });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);
            Assert.Equal("file-contents", File.ReadAllText(file));
            File.Delete(file);
        }

        [Fact]
        public async Task CopyFileRLLocalErrorTestAsync()
        {
            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);

            var file = Path.GetTempFileName();
            File.SetAttributes(file, FileAttributes.ReadOnly);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "/home/mizu/machete/raw3", TargetPath = file } };
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", default, default) } });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });

            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access to path {file} on the local machine is denied", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyFileLRRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = Path.GetTempFileName(), TargetPath = "/home/mizu/machete/raw3" } };

            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", default, default) } });
            channel.ThenRespond(new PutFileResponse { Status = PutFileStatus.PermissionDenied });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal("Access to path /home/mizu/machete/raw3 on the remote machine is denied", result.StepResults[0].Warning);

            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", default, default) } });
            channel.ThenRespond(new PutFileResponse { Status = PutFileStatus.OtherIOError });
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal("File /home/mizu/machete/raw3 could not be created on the remote machine", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyFileLocalErrorTestAsync()
        {
            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);

            var localPath = Path.GetTempFileName();
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToLocal, SourcePath = localPath, FailIfNotModified = true, TargetPath = Path.GetRandomFileName() } };
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal("File was not changed after executing the previous steps. Disable Check Timestamp in step options to skip the modification date check.", result.StepResults[0].Warning);

            var nonexistentPath = @"C:\Non\Existent\Path\To\Users\mizu\raw3";
            steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToLocal, SourcePath = nonexistentPath, FailIfNotModified = true, TargetPath = Path.GetRandomFileName() } };
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($@"Path ""C:\Non\Existent\Path\To\Users\mizu\raw3"" does not exist", result.StepResults[0].Warning);

            var lockedPath = Path.GetTempFileName();
            var acl = File.GetAccessControl(lockedPath);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.Read, AccessControlType.Deny));
            File.SetAccessControl(lockedPath, acl);

            steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToLocal, SourcePath = lockedPath, TargetPath = Path.GetRandomFileName() } };
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access to path {lockedPath} on the local machine is denied", result.StepResults[0].Warning);
            File.Delete(lockedPath);
        }

        [Fact]
        public async Task CopyFileLLTestAsync()
        {
            var runner = new ActionRunner(null, MockController(), null);

            var file = Path.GetTempFileName();
            var target = Path.GetTempFileName();
            File.WriteAllText(file, "local to local copy test");
            var steps = new List<IActionStep>
            {
                // update file timestamp
                new ExecuteStep { Environment = StepEnvironment.Local, Executable = "cmd.exe", Arguments = $@"/C ""copy /b {Path.GetFileName(file)} +,,""", WorkingDirectory = Path.GetDirectoryName(file) },
                new CopyFileStep { Direction = FileCopyDirection.LocalToLocal, SourcePath = file, TargetPath = target, FailIfNotModified = true }
            };

            var result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);

            Assert.Equal("local to local copy test", File.ReadAllText(target));
        }

        [Fact]
        public async Task CopyDirectoryLRTestAsync()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpDir);
            Directory.CreateDirectory(tmpDir + "\\empty");
            Directory.SetLastWriteTimeUtc(tmpDir + "\\empty", new DateTime(1970, 1, 1));
            File.WriteAllText(tmpDir + "\\t", "test");
            File.WriteAllText(tmpDir + "\\t2", "test2");
            File.SetLastWriteTimeUtc(tmpDir + "\\t", new DateTime(1980, 1, 1));
            File.SetLastWriteTimeUtc(tmpDir + "\\t2", new DateTime(1990, 1, 1));

            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = tmpDir, TargetPath = "/home/mizu/rawdir", SkipIfNotModified = true, IncludeSubdirectories = true } };

            // t is unchanged, t2's size is different, empty/ is missing
            channel.ThenRespond(new ListFilesResponse
            {
                Files = new[]
                {
                    new FileMetadata("./", default, default),
                    new FileMetadata("t", 4, new DateTime(1980, 1, 1)),
                    new FileMetadata("t2", 4, new DateTime(1990, 1, 1))
                }
            }, (ListFilesCommand command) =>
            {
                Assert.Equal("/home/mizu/rawdir", command.Path);
            });
            channel.ThenRespond(new PutDirectoryResponse { Status = PutDirectoryStatus.Successful }, (PutDirectoryCommand command) =>
            {
                Assert.Equal(2, command.Files.Length);
                Assert.Equal("t2", command.Files[0].RelativePath);
                Assert.Equal("test2", Encoding.UTF8.GetString(command.Files[0].Data));
                Assert.Equal("empty/", command.Files[1].RelativePath);
                Assert.Equal(Array.Empty<byte>(), command.Files[1].Data);
            });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);

            Directory.Delete(tmpDir, recursive: true);
        }

        [Fact]
        public async Task CopyDirectoryLRErrorsTestAsync()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = tmpDir, TargetPath = "/home/mizu/rawdir", SkipIfNotModified = true } };

            // Path does not exist
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($@"Path ""{tmpDir}"" does not exist", result.StepResults[0].Warning);

            // Permission denied
            Directory.CreateDirectory(tmpDir);
            var acl = Directory.GetAccessControl(tmpDir);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.ListDirectory, AccessControlType.Deny));
            Directory.SetAccessControl(tmpDir, acl);

            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access to directory or its contents is denied: \"{tmpDir}\"", result.StepResults[0].Warning);

            acl.RemoveAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.ListDirectory, AccessControlType.Deny));
            Directory.SetAccessControl(tmpDir, acl);
            Directory.Delete(tmpDir, recursive: true);
        }

        [Fact]
        public async Task CopyDirectoryRLTestAsync()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpDir);
            File.WriteAllText(tmpDir + "\\t", "test");
            File.WriteAllText(tmpDir + "\\t2", "test2");
            File.SetLastWriteTimeUtc(tmpDir + "\\t", new DateTime(1980, 1, 1));
            File.SetLastWriteTimeUtc(tmpDir + "\\t2", new DateTime(1990, 1, 1));

            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "/home/mizu/rawdir", TargetPath = tmpDir, SkipIfNotModified = true } };

            // t is unchanged, t2's size is different
            channel.ThenRespond(new ListFilesResponse
            {
                Files = new[]
                {
                    new FileMetadata("./", default, default),
                    new FileMetadata("t", 4, new DateTime(1980, 1, 1)),
                    new FileMetadata("t2", 4, new DateTime(1990, 1, 1))
                }
            }, (ListFilesCommand command) =>
            {
                Assert.Equal("/home/mizu/rawdir", command.Path);
            });

            var files = new[] { new PackedFile(new byte[] { 0, 1, 2, 3 }, "t2", new DateTime(1990, 1, 1)) };
            channel.ThenRespond(new GetFilesResponse { Status = GetFilesStatus.Successful, Files = files }, (GetFilesCommand command) =>
            {
                Assert.Equal("/home/mizu/rawdir", command.RootPath);
                Assert.Equal(new[] { "t2" }, command.Paths);
            });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);

            Directory.Delete(tmpDir, recursive: true);
        }

        [Fact]
        public async Task CopyDirectoryRLErrorsTestAsync()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpDir);
            File.WriteAllText(tmpDir + "\\t", "test");

            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "/home/mizu/rawdir", TargetPath = tmpDir, SkipIfNotModified = true } };

            // t's size is changed => it'll be requested
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata("./", default, default), new FileMetadata("t", 1, default) } });
            var files = new[] { new PackedFile(new byte[] { 0, 1, 2, 3 }, "t", new DateTime(1990, 1, 1)) };
            channel.ThenRespond(new GetFilesResponse { Status = GetFilesStatus.Successful, Files = files });

            // Permission denied
            File.SetAttributes(tmpDir + "\\t", FileAttributes.ReadOnly);

            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access to path \"{tmpDir}\" on the local machine is denied", result.StepResults[0].Warning);

            File.SetAttributes(tmpDir + "\\t", FileAttributes.Normal);
            Directory.Delete(tmpDir, recursive: true);
        }
        #endregion

        #region ExecuteStep
        [Fact]
        public async Task ExecuteRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep> { new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "dvd-prepare" } };
            var controller = new Mock<IActionRunController>();
            var runner = new ActionRunner(channel, controller.Object, null);

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch, Stdout = "", Stderr = "The directory name is invalid." });
            var result = await runner.RunAsync("UFOW", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("dvd-prepare process could not be started on the remote machine. The directory name is invalid.", result.StepResults[0].Warning);
            Assert.Equal("The directory name is invalid.", result.StepResults[0].Log);

            controller.Setup(c => c.ShouldTerminateProcessOnTimeoutAsync(It.IsAny<IList<ProcessTreeItem>>())).ReturnsAsync(true);
            channel.ThenRespond(new ExecutionTimedOutResponse { ProcessTree = new[] { new ProcessTreeItem(1, "systemd", 0) } });
            channel.ThenRespond(new ExecutionTerminatedResponse { TerminatedProcessTree = new[] { new ProcessTreeItem(1, "systemd", 0) } },
               (ExecutionTimedOutActionCommand c) => Assert.True(c.TerminateProcesses));
            result = await runner.RunAsync("UFOW", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Execution timeout is exceeded. dvd-prepare process on the remote machine is terminated.", result.StepResults[0].Warning);
            Assert.Equal("The following processes were terminated:\r\n [1] systemd\r\n", result.StepResults[0].Log);

            controller.Setup(c => c.ShouldTerminateProcessOnTimeoutAsync(It.IsAny<IList<ProcessTreeItem>>())).ReturnsAsync(false);
            channel.ThenRespond(new ExecutionTimedOutResponse { ProcessTree = new[] { new ProcessTreeItem(1, "systemd", 0) } });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 },
               (ExecutionTimedOutActionCommand c) => Assert.False(c.TerminateProcesses));
            result = await runner.RunAsync("UFOW", steps, false);
            Assert.True(result.Successful);

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
                new ExecuteStep { Environment = StepEnvironment.Local, Executable = "python.exe", Arguments = $"-c \"print('success', file=open(r'{file}', 'w'))\"", WorkingDirectory = Path.GetTempPath() }
            };
            var runner = new ActionRunner(null, MockController(), null);
            var result = await runner.RunAsync("", steps);
            Assert.True(result.Successful);
            Assert.Equal("", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (exit code 0)\r\n", result.StepResults[0].Log);

            var output = File.ReadAllText(file);
            File.Delete(file);
            Assert.Equal("success\r\n", output);
        }

        [Fact]
        public async Task ExecuteRemoteWorkingDirectoryTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep> { new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "exe", WorkingDirectory = "/action/env/remote/dir" } };
            var runner = new ActionRunner(channel, MockController(), null);

            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted(), command =>
            {
                Assert.Equal("/action/env/remote/dir", command.WorkingDirectory);
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
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, FailIfNotModified = true, SourcePath = "/home/mizu/machete/tweened.tvpp", TargetPath = Path.GetTempFileName() }
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
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", default, DateTime.FromBinary(101)) } });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });
            var runner = new ActionRunner(channel, MockController(), null);
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
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/glitch/city/output", CheckTimestamp = true },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/glitch/city/watches", CheckTimestamp = false },
                    dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/glitch/city/status", CheckTimestamp = false },
                    binaryOutput: true, outputOffset: 0)
            };

            var channel = new MockCommunicationChannel(DebugServer.IPC.ServerPlatform.Linux);
            var runner = new ActionRunner(channel, MockController(), null);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata initTimestampFetch) =>
                Assert.Equal(new[] { "/glitch/city/output" }, initTimestampFetch.FilePath));
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("jill\njulianne") }, (FetchResultRange watchesFetch) =>
                Assert.Equal(new[] { "/glitch/city/watches" }, watchesFetch.FilePath));
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes(@"
grid_size (8192, 0, 0)
group_size (512, 0, 0)
wave_size 32
comment 115200") }, (FetchResultRange statusFetch) =>
                Assert.Equal(new[] { "/glitch/city/status" }, statusFetch.FilePath));
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now }, (FetchMetadata outputMetaFetch) =>
                Assert.Equal(new[] { "/glitch/city/output" }, outputMetaFetch.FilePath));

            var result = await runner.RunAsync("Debug", steps);

            Assert.True(channel.AllInteractionsHandled);
            Assert.True(result.Successful);
            Assert.NotNull(result.BreakState);
            Assert.Collection(result.BreakState.Data.Watches,
                (first) => Assert.Equal("jill", first),
                (second) => Assert.Equal("julianne", second));
            Assert.NotNull(result.BreakState.DispatchParameters);
            Assert.Equal<uint>(8192 / 512, result.BreakState.DispatchParameters.DimX);
            Assert.Equal<uint>(512, result.BreakState.DispatchParameters.GroupSizeX);
            Assert.Equal<uint>(32, result.BreakState.DispatchParameters.WaveSize);
            Assert.False(result.BreakState.DispatchParameters.NDRange3D);
            Assert.Equal("115200", result.BreakState.DispatchParameters.StatusString);
        }

        [Fact]
        public async Task ReadDebugDataRemoteTextOutputWarningsTestAsync()
        {
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "va11" },
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/glitch/city/output", CheckTimestamp = true },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/glitch/city/watches", CheckTimestamp = false },
                    dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/glitch/city/status", CheckTimestamp = false },
                    binaryOutput: false, outputOffset: 1)
            };

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel, MockController(), null);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("jill\njulianne\nstingray") });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes(@"
grid_size (16384, 0, 0)
group_size (256, 0, 0)
wave_size 64") });
            // Note that the file is missing the output offset line (it contains 262144/4 = 65536 values, which, with the offset subtracted, is only 65535 values.
            // This may cause an out of range error in ChangeGroupWithWarningsAsync
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, ByteCount = 262144 }, (FetchMetadata outputMetaFetch) =>
                Assert.False(outputMetaFetch.BinaryOutput));

            var result = await runner.RunAsync("Debug", steps);

            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = new byte[262140] }, (FetchResultRange outputFetch) =>
            {
                Assert.Equal(0, outputFetch.ByteOffset);
                Assert.Equal(262140, outputFetch.ByteCount);
                Assert.False(outputFetch.BinaryOutput);
                Assert.Equal(1, outputFetch.OutputOffset);
            });
            _ = await result.BreakState.Data.ChangeGroupWithWarningsAsync(channel, groupIndex: 0, groupSize: 256, waveSize: 64, nGroups: 0, fetchWholeFile: true);

            Assert.True(result.Successful);
            Assert.True(channel.AllInteractionsHandled);
            Assert.Equal(@"Output file (/glitch/city/output) is smaller than expected.

Grid size as specified in the dispatch parameters file is (16384, 1, 1), which corresponds to 16384 lanes. With 4 DWORDs per lane, the output file is expected to contain at least 65536 DWORDs, but it only contains 65535 DWORDs.",
            result.StepResults[1].Warning);
        }

        [Fact]
        public async Task ReadDebugDataRemoteErrorTestAsync()
        {
            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/remote/output", CheckTimestamp = true },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/remote/watches", CheckTimestamp = true },
                    dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/remote/status", CheckTimestamp = true },
                    binaryOutput: true, outputOffset: 0)
            };

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel, MockController(), null);

            /* File not found */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(i) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.FileNotFound }, (FetchResultRange w) => Assert.Equal(new[] { "/remote/watches" }, w.FilePath));

            var result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Valid watches file (/remote/watches) could not be found.", result.StepResults[0].Warning);

            /* File not changed */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(i) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(0) },
                (FetchResultRange w) => Assert.Equal(new[] { "/remote/watches" }, w.FilePath));

            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Valid watches file (/remote/watches) was not modified.", result.StepResults[0].Warning);
        }

        [Theory]
        [InlineData("grid_size (128, 0, 0)\ngroup_size (64, 0, 0)\nwave_size 64", "")] /* file size (384*4 bytes) matches grid size, no warning shown */
        [InlineData("grid_size (64, 0, 0)\ngroup_size (64, 0, 0)\nwave_size 64", "")] /* file size exceeds grid size, no warning shown */
        [InlineData("grid_size (256, 0, 0)\ngroup_size (64, 0, 0)\nwave_size 64", /* grid size exceeds file size, "output file is smaller than expected" warning shown */
            "Output file ({outputFile}) is smaller than expected.\r\n\r\nGrid size as specified in the dispatch parameters file is (256, 1, 1), which corresponds to 256 lanes. " +
            "With 3 DWORDs per lane, the output file is expected to contain at least 768 DWORDs, but it only contains 384 DWORDs.")]
        public async Task ReadDebugDataLocalWarningsTestAsync(string dispatchParams, string expectedWarning)
        {
            var outputFile = Path.GetTempFileName();
            var watchesFile = Path.GetTempFileName();
            var dispatchParamsFile = Path.GetTempFileName();

            File.WriteAllText(watchesFile, "jill\r\njulianne");
            File.WriteAllText(dispatchParamsFile, dispatchParams);
            var output = new uint[384];
            for (int witem = 0; witem < 128; ++witem)
            {
                output[witem * 3 + 0] = 1;                // system = const
                output[witem * 3 + 1] = 777;              // first watch = const
                output[witem * 3 + 2] = (uint)witem % 64; // second watch = lane
            }
            var outputOffset = 3;
            byte[] outputBytes = new byte[output.Length * sizeof(int) + outputOffset];
            Buffer.BlockCopy(output, 0, outputBytes, outputOffset, outputBytes.Length - outputOffset);
            File.WriteAllBytes(outputFile, outputBytes);

            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = outputFile, CheckTimestamp = false },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = watchesFile, CheckTimestamp = false },
                    dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = dispatchParamsFile, CheckTimestamp = false },
                    binaryOutput: true, outputOffset)
            };
            var runner = new ActionRunner(null, MockController(), null);
            var result = await runner.RunAsync("Debug", steps);

            Assert.True(result.Successful);
            Assert.Equal(expectedWarning.Replace("{outputFile}", outputFile), result.StepResults[0].Warning);
            Assert.NotNull(result.BreakState);
            Assert.Collection(result.BreakState.Data.Watches,
                (first) => Assert.Equal("jill", first),
                (second) => Assert.Equal("julianne", second));
            Assert.Equal<uint>(64, result.BreakState.DispatchParameters.GroupSizeX);
            Assert.Equal<uint>(64, result.BreakState.DispatchParameters.WaveSize);

            await result.BreakState.Data.ChangeGroupWithWarningsAsync(null, groupIndex: 0, groupSize: 64, waveSize: 64, nGroups: 0);
            var secondWatch = result.BreakState.Data.GetWatch("julianne");
            for (int i = 0; i < 64; ++i)
                Assert.Equal(i, (int)secondWatch[i]);
        }

        [Theory]
        [InlineData("grid_size (4, 0, 0)\ngroup_size (2, 0, 0)\nwave_size 2", "")] /* file size matches grid size, no warning shown */
        [InlineData("grid_size (2, 0, 0)\ngroup_size (2, 0, 0)\nwave_size 2", "")] /* file size exceeds grid size, no warning shown */
        [InlineData("grid_size (8, 0, 0)\ngroup_size (2, 0, 0)\nwave_size 2", /* grid size exceeds file size, "output file is smaller than expected" warning shown */
            "Output file ({outputFile}) is smaller than expected.\r\n\r\nGrid size as specified in the dispatch parameters file is (8, 1, 1), which corresponds to 8 lanes. " +
            "With 2 DWORDs per lane, the output file is expected to contain at least 16 DWORDs, but it only contains 8 DWORDs.")]
        public async Task ReadDebugDataLocalTextOutputWarningsTestAsync(string dispatchParams, string expectedWarning)
        {
            var outputFile = Path.GetTempFileName();
            var dispatchParamsFile = Path.GetTempFileName();
            File.WriteAllLines(outputFile, new[] { "SKIPPED LINE", "0x6173616b", "0x7573616d", "0x69646f72", "0x69333133", "0x0", "0x0", "0x0", "0x0" });
            File.WriteAllText(dispatchParamsFile, dispatchParams);

            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = outputFile, CheckTimestamp = false },
                    watchesFile: new BuiltinActionFile(),
                    dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = dispatchParamsFile, CheckTimestamp = false },
                    binaryOutput: false, outputOffset: 1)
            };
            var runner = new ActionRunner(null, MockController(), new ReadOnlyCollection<string>(new[] { "const" }));
            var result = await runner.RunAsync("Debug", steps);

            Assert.True(result.Successful);
            Assert.Equal(expectedWarning.Replace("{outputFile}", outputFile), result.StepResults[0].Warning);

            await result.BreakState.Data.ChangeGroupWithWarningsAsync(null, groupIndex: 0, groupSize: 2, waveSize: 2, nGroups: 0);
            var system = result.BreakState.Data.GetSystem();
            Assert.Equal(0x6173616b, (int)system[0]);
            Assert.Equal(0x69646f72, (int)system[1]);
            var constWatch = result.BreakState.Data.GetWatch("const");
            Assert.Equal(0x7573616d, (int)constWatch[0]);
            Assert.Equal(0x69333133, (int)constWatch[1]);
        }

        [Fact]
        public async Task ReadDebugDataLocalErrorTestAsync()
        {
            var outputFile = Path.GetTempFileName();

            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = outputFile, CheckTimestamp = true },
                    watchesFile: new BuiltinActionFile(),
                    dispatchParamsFile: new BuiltinActionFile(),
                    binaryOutput: true, outputOffset: 0)
            };

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel, MockController(), null);

            /* File not changed (GetTempFileName creates an empty file) */

            var result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Output file ({outputFile}) was not modified. Data may be stale.", result.StepResults[0].Warning);

            /* Access denied */

            var acl = File.GetAccessControl(outputFile);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.Read, AccessControlType.Deny));
            File.SetAccessControl(outputFile, acl);

            ((ReadDebugDataStep)steps[0]).OutputFile.CheckTimestamp = false;
            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Output file could not be opened. Access to path {outputFile} on the local machine is denied", result.StepResults[0].Warning);

            /* File not found */

            File.Delete(outputFile);
            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Output file could not be opened. File {outputFile} is not found on the local machine", result.StepResults[0].Warning);
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
            readDebugData.WatchesFile.Location = StepEnvironment.Remote;
            readDebugData.WatchesFile.CheckTimestamp = false;
            readDebugData.WatchesFile.Path = "/home/parker/audio/copy";
            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, FailIfNotModified = true, SourcePath = "/home/parker/audio/checked", TargetPath = Path.GetTempFileName() },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, FailIfNotModified = false, SourcePath = "/home/parker/audio/unchecked", TargetPath = Path.GetTempFileName() },
                readDebugData
            };

            var runner = new ActionRunner(channel, MockController(), null);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(100) }, (FetchMetadata command) =>
                Assert.Equal(new[] { "/home/parker/audio/checked" }, command.FilePath));
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata command) =>
                Assert.Equal(new[] { "/home/parker/audio/master" }, command.FilePath));

            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", 1, DateTime.FromBinary(101)) } },
                (ListFilesCommand command) => { Assert.Equal("/home/parker/audio/checked", command.Path); });
            channel.ThenRespond(new ResultRangeFetched { Data = Encoding.UTF8.GetBytes("TestCopyStepChecked") },
                (FetchResultRange command) => Assert.Equal(new[] { "/home/parker/audio/checked" }, command.FilePath));
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", 1, DateTime.FromBinary(101)) } },
                (ListFilesCommand command) => { Assert.Equal("/home/parker/audio/unchecked", command.Path); });
            channel.ThenRespond(new ResultRangeFetched { Data = Encoding.UTF8.GetBytes("TestCopyStepUnchecked") },
                (FetchResultRange command) => Assert.Equal(new[] { "/home/parker/audio/unchecked" }, command.FilePath));

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
