using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class ActionRunnerTests
    {
        private readonly IProject _project = new Mock<IProject>().Object;

        [Fact]
        public async Task SucessfulRunTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, IfNotModified = ActionIfNotModified.Fail, SourcePath = "/home/mizu/machete/tweened.tvpp", TargetPath = Path.GetTempFileName() }
            };
            var localTempFile = Path.GetRandomFileName();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);

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
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);
            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, IfNotModified = ActionIfNotModified.Fail, SourcePath = "/home/mizu/machete/key3_49", TargetPath = Path.GetRandomFileName() },
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" } // should not be run
            };

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }); // init timestamp fetch
            channel.ThenRespond(new ListFilesResponse { Files = Array.Empty<FileMetadata>() });
            var result = await runner.RunAsync("HTMT", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Data is missing. File or directory is not found on the remote machine at /home/mizu/machete/key3_49", result.StepResults[0].Warning);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) }); // init timestamp fetch
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", 1, DateTime.FromBinary(100)) } });
            result = await runner.RunAsync("HTMT", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Data is stale. File was not modified on the remote machine at /home/mizu/machete/key3_49", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyFileRLMissingParentDirectoryTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);

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
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);

            var file = Path.GetTempFileName();
            File.SetAttributes(file, FileAttributes.ReadOnly);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "/home/mizu/machete/raw3", TargetPath = file } };
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", default, default) } });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });

            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access is denied to local file at {file}", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyFileLRRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = Path.GetTempFileName(), TargetPath = "/home/mizu/machete/raw3" } };

            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", default, default) } });
            channel.ThenRespond(new PutFileResponse { Status = PutFileStatus.PermissionDenied });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal("Access is denied to remote file at /home/mizu/machete/raw3", result.StepResults[0].Warning);

            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", default, default) } });
            channel.ThenRespond(new PutFileResponse { Status = PutFileStatus.OtherIOError });
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal("Cannot create file on the remote machine at /home/mizu/machete/raw3", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyFileLocalErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);

            var localPath = Path.GetTempFileName();
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToLocal, SourcePath = localPath, IfNotModified = ActionIfNotModified.Fail, TargetPath = Path.GetRandomFileName() } };
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Data is stale. File was not modified on the local machine at {localPath}", result.StepResults[0].Warning);

            var nonexistentPath = @"C:\Non\Existent\Path\To\Users\mizu\raw3";
            steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToLocal, SourcePath = nonexistentPath, IfNotModified = ActionIfNotModified.Fail, TargetPath = Path.GetRandomFileName() } };
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal(@"Data is missing. File or directory is not found on the local machine at C:\Non\Existent\Path\To\Users\mizu\raw3", result.StepResults[0].Warning);

            var lockedPath = Path.GetTempFileName();
            var acl = File.GetAccessControl(lockedPath);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.Read, AccessControlType.Deny));
            File.SetAccessControl(lockedPath, acl);

            steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToLocal, SourcePath = lockedPath, TargetPath = Path.GetRandomFileName() } };
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access is denied to local file at {lockedPath}", result.StepResults[0].Warning);
            File.Delete(lockedPath);
        }

        [Fact]
        public async Task CopyFileLLTestAsync()
        {
            var runner = new ActionRunner(null, null, new ActionEnvironment(), _project);

            var file = Path.GetTempFileName();
            var target = Path.GetTempFileName();
            File.WriteAllText(file, "local to local copy test");
            var steps = new List<IActionStep>
            {
                // update file timestamp
                new ExecuteStep { Environment = StepEnvironment.Local, Executable = "cmd.exe", Arguments = $@"/C ""copy /b {Path.GetFileName(file)} +,,""", WorkingDirectory = Path.GetDirectoryName(file) },
                new CopyFileStep { Direction = FileCopyDirection.LocalToLocal, SourcePath = file, TargetPath = target, IfNotModified = ActionIfNotModified.Fail }
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

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = tmpDir, TargetPath = "/home/mizu/rawdir", IfNotModified = ActionIfNotModified.DoNotCopy, IncludeSubdirectories = true } };

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

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = tmpDir, TargetPath = "/home/mizu/rawdir", IfNotModified = ActionIfNotModified.DoNotCopy } };

            // Path does not exist
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Data is missing. File or directory is not found on the local machine at {tmpDir}", result.StepResults[0].Warning);

            // Permission denied
            Directory.CreateDirectory(tmpDir);
            var acl = Directory.GetAccessControl(tmpDir);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.ListDirectory, AccessControlType.Deny));
            Directory.SetAccessControl(tmpDir, acl);

            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access to a local directory or its contents is denied at {tmpDir}", result.StepResults[0].Warning);

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

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "/home/mizu/rawdir", TargetPath = tmpDir, IfNotModified = ActionIfNotModified.DoNotCopy } };

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

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "/home/mizu/rawdir", TargetPath = tmpDir, IfNotModified = ActionIfNotModified.DoNotCopy } };

            // t's size is changed => it'll be requested
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata("./", default, default), new FileMetadata("t", 1, default) } });
            var files = new[] { new PackedFile(new byte[] { 0, 1, 2, 3 }, "t", new DateTime(1990, 1, 1)) };
            channel.ThenRespond(new GetFilesResponse { Status = GetFilesStatus.Successful, Files = files });

            // Permission denied
            File.SetAttributes(tmpDir + "\\t", FileAttributes.ReadOnly);

            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Access is denied to local file at {tmpDir}", result.StepResults[0].Warning);

            File.SetAttributes(tmpDir + "\\t", FileAttributes.Normal);
            Directory.Delete(tmpDir, recursive: true);
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
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, IfNotModified = ActionIfNotModified.Copy, TargetPath = "/home/parker/audio/unchecked", SourcePath = "" }, // should not be run
            };
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);

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
                new ExecuteStep { Environment = StepEnvironment.Local, Executable = "python.exe", Arguments = $"-c \"print('success', file=open(r'{file}', 'w'))\"", WorkingDirectory = Path.GetTempPath() }
            };
            var runner = new ActionRunner(channel: null, serviceProvider: null, new ActionEnvironment(), _project);
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
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);

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
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "output", CheckTimestamp = false },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "watches", CheckTimestamp = false },
                    dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "status", CheckTimestamp = false },
                    binaryOutput: true, outputOffset: 0, magicNumber: null)
            };
            var level2Steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" },
                new RunActionStep(level3Steps) { Name = "level3" }
            };
            var level1Steps = new List<IActionStep>
            {
                new RunActionStep(level2Steps) { Name = "level2" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, IfNotModified = ActionIfNotModified.Fail, SourcePath = "/home/mizu/machete/tweened.tvpp", TargetPath = Path.GetTempFileName() }
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
            // 4. Level 3 Read Debug Data
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("ValidWatches.txt") });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("DispatchParams.txt") });
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, ByteCount = TestHelper.GetFixtureSize("DebugBuffer.bin") });
            // 4. Level 1 Copy File
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata(".", default, DateTime.FromBinary(101)) } });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")), _project);
            var result = await runner.RunAsync("HTMT", level1Steps);

            Assert.True(result.Successful);
            Assert.NotNull(result.BreakState);
            Assert.Equal("level2", result.StepResults[0].SubAction.ActionName);
            Assert.Null(result.StepResults[0].SubAction.StepResults[0].SubAction);
            Assert.Equal("Captured stdout (exit code 0):\r\nlevel2\r\n", result.StepResults[0].SubAction.StepResults[0].Log);
            Assert.Equal("level3", result.StepResults[0].SubAction.StepResults[1].SubAction.ActionName);
            Assert.Equal("Captured stdout (exit code 0):\r\nlevel3\r\n", result.StepResults[0].SubAction.StepResults[1].SubAction.StepResults[0].Log);
            Assert.Null(result.StepResults[1].SubAction);
        }


        [Fact]
        public async Task WriteDebugTargetStepTestAsync()
        {
            var (breakpointListFile, watchListFile) = (Path.GetTempFileName(), Path.GetTempFileName());
            var steps = new List<IActionStep> { new WriteDebugTargetStep { BreakpointListPath = breakpointListFile, WatchListPath = watchListFile } };
            var watches = new[] { "a", "c", "tide" };
            var breakpoints = new[] { new BreakpointInfo(@"C:\Source.s", 139, 1, false), new BreakpointInfo(@"C:\Include.s", 313, 1, true) };
            var breakTarget = new BreakTarget(breakpoints, BreakTargetSelector.Multiple, @"C:\PrevFile.s", 471, @"C:\Main.s");
            var runner = new ActionRunner(null, null, new ActionEnvironment(watches: watches, breakTarget: breakTarget), _project);
            var result = await runner.RunAsync("Debug", steps);

            Assert.True(result.Successful);
            Assert.Equal("", result.StepResults[0].Warning);

            var breakpointListJson = File.ReadAllText(breakpointListFile);
            Assert.Equal(@"{
  ""Breakpoints"": [
    {
      ""File"": ""C:\\Source.s"",
      ""Line"": 139,
      ""HitCountTarget"": 1,
      ""StopOnHit"": 0
    },
    {
      ""File"": ""C:\\Include.s"",
      ""Line"": 313,
      ""HitCountTarget"": 1,
      ""StopOnHit"": 1
    }
  ],
  ""Select"": ""Multiple"",
  ""PrevTargetFile"": ""C:\\PrevFile.s"",
  ""PrevTargetLine"": 471
}", breakpointListJson);

            var watchListLines = File.ReadAllText(watchListFile);
            Assert.Equal("a\r\nc\r\ntide", watchListLines);
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
                    binaryOutput: true, outputOffset: 0, magicNumber: null)
            };

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")), _project);

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata initTimestampFetch) =>
                Assert.Equal(new[] { "/glitch/city/output" }, initTimestampFetch.FilePath));
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("ValidWatches.txt") }, (FetchResultRange watchesFetch) =>
                Assert.Equal(new[] { "/glitch/city/watches" }, watchesFetch.FilePath));
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("DispatchParams.txt") }, (FetchResultRange statusFetch) =>
                Assert.Equal(new[] { "/glitch/city/status" }, statusFetch.FilePath));
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, ByteCount = TestHelper.GetFixtureSize("DebugBuffer.bin") }, (FetchMetadata outputMetaFetch) =>
                Assert.Equal(new[] { "/glitch/city/output" }, outputMetaFetch.FilePath));

            var result = await runner.RunAsync("Debug", steps);

            Assert.True(channel.AllInteractionsHandled);
            Assert.True(result.Successful);
            Assert.NotNull(result.BreakState);
            Assert.NotNull(result.BreakState.Dispatch);
            Assert.Equal(16384u / 512, result.BreakState.Dispatch.NumGroupsX);
            Assert.Equal(512u, result.BreakState.Dispatch.GroupSizeX);
            Assert.Equal(64u, result.BreakState.Dispatch.WaveSize);
            Assert.False(result.BreakState.Dispatch.NDRange3D);
            Assert.Equal("115200", result.BreakState.Dispatch.StatusString);
        }

        [Fact]
        public async Task ReadDebugDataRemoteErrorTestAsync()
        {
            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/remote/output", CheckTimestamp = true },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/remote/watches", CheckTimestamp = true },
                    dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "/remote/dispatch", CheckTimestamp = true },
                    binaryOutput: true, outputOffset: 0, magicNumber: null)
            };

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")), _project);

            /* File not found */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(i) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.FileNotFound }, (FetchResultRange w) => Assert.Equal(new[] { "/remote/watches" }, w.FilePath));

            var result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Valid watches data is missing. File could not be found on the remote machine at /remote/watches", result.StepResults[0].Warning);

            /* File not changed */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(i) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(0) },
                (FetchResultRange w) => Assert.Equal(new[] { "/remote/watches" }, w.FilePath));

            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Valid watches data is stale. File was not modified by the debug action on the remote machine at /remote/watches", result.StepResults[0].Warning);

            /* Wrong output file size */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(0) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, Data = TestHelper.ReadFixtureBytes("ValidWatches.txt") },
                (FetchResultRange f) => Assert.Equal(new[] { "/remote/watches" }, f.FilePath));
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, Data = TestHelper.ReadFixtureBytes("DispatchParams.txt") },
                (FetchResultRange f) => Assert.Equal(new[] { "/remote/dispatch" }, f.FilePath));
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, ByteCount = 65536 },
                (FetchMetadata f) => Assert.Equal(new[] { "/remote/output" }, f.FilePath));

            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal(@"Debug data is invalid. Output file does not match the expected size.

Grid size as specified in the dispatch parameters file is (16384, 1, 1), or 16384 lanes in total. With 7 DWORDs per lane, the output file is expected to be 114688 DWORDs long, but the actual size is 16384 DWORDs.",
result.StepResults[0].Warning);
        }

        [Fact]
        public async Task ReadDebugDataLocalTestAsync()
        {
            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = TestHelper.GetFixturePath("DebugBuffer.bin"), CheckTimestamp = false },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = TestHelper.GetFixturePath("ValidWatches.txt"), CheckTimestamp = false },
                    dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = TestHelper.GetFixturePath("DispatchParams.txt"), CheckTimestamp = false },
                    binaryOutput: true, outputOffset: 0, magicNumber: null)
            };
            var runner = new ActionRunner(null, null, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")), _project);
            var result = await runner.RunAsync("Debug", steps);

            Assert.True(result.Successful);
            Assert.Equal("", result.StepResults[0].Warning);
            Assert.NotNull(result.BreakState);
            Assert.Equal(16384u, result.BreakState.Dispatch.GridSizeX);
            Assert.Equal(512u, result.BreakState.Dispatch.GroupSizeX);
            Assert.Equal(64u, result.BreakState.Dispatch.WaveSize);
            Assert.Equal(7u, result.BreakState.DwordsPerLane);
            Assert.Equal(new[] { "tid", "lst", "a", "c", "tide", "lst[1]" }, result.BreakState.Watches.Keys);

            Assert.Equal((Instance: 0, DataSlot: 2, ListSize: null), result.BreakState.Watches["c"].Instances[0]);
            Assert.Equal((Instance: 1, DataSlot: null, ListSize: 6), result.BreakState.Watches["c"].Instances[1]);
            Assert.Equal(6, result.BreakState.Watches["c"].ListItems.Count);
            Assert.Empty(result.BreakState.Watches["c"].ListItems[0].Instances);
            Assert.Equal((Instance: 1, DataSlot: null, ListSize: 2), result.BreakState.Watches["c"].ListItems[1].Instances[0]);
            Assert.Equal((Instance: 1, DataSlot: 3, ListSize: null), result.BreakState.Watches["c"].ListItems[1].ListItems[0].Instances[0]);
            Assert.Equal((Instance: 1, DataSlot: 4, ListSize: null), result.BreakState.Watches["c"].ListItems[1].ListItems[1].Instances[0]);
            Assert.Equal((Instance: 1, DataSlot: null, ListSize: 1), result.BreakState.Watches["c"].ListItems[3].Instances[0]);
            Assert.Empty(result.BreakState.Watches["c"].ListItems[3].ListItems[0].Instances);
            Assert.Equal((Instance: 1, DataSlot: null, ListSize: 0), result.BreakState.Watches["c"].ListItems[4].Instances[0]);
            Assert.Equal((Instance: 1, DataSlot: 5, ListSize: null), result.BreakState.Watches["c"].ListItems[5].Instances[0]);

            var (tidInstance, tidSlot, tidListSize) = result.BreakState.Watches["tid"].Instances.First();
            Assert.Equal(1u, tidInstance);
            Assert.Equal(1u, tidSlot);
            Assert.Null(tidListSize);

            await result.BreakState.ChangeGroupWithWarningsAsync(null, groupIndex: tidInstance);
            var tidData = result.BreakState.GetWatchData(4, (uint)tidSlot);
            for (int i = 0; i < 64; ++i)
                Assert.Equal(4u * 64 + i, tidData[i]);
        }

        [Fact]
        public async Task ReadDebugDataLocalErrorTestAsync()
        {
            string outputFile = Path.GetTempFileName(), watchesFile = Path.GetTempFileName(), dispatchFile = Path.GetTempFileName();

            var steps = new List<IActionStep>
            {
                new ReadDebugDataStep(
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = outputFile, CheckTimestamp = true },
                    watchesFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = watchesFile, CheckTimestamp = false },
                    dispatchParamsFile: new BuiltinActionFile { Location = StepEnvironment.Local, Path = dispatchFile, CheckTimestamp = false },
                    binaryOutput: true, outputOffset: 0, magicNumber: null)
            };

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")), _project);

            /* File not changed (GetTempFileName creates an empty file) */

            var result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Debug data is stale. Output file was not modified by the debug action on the local machine at {outputFile}", result.StepResults[0].Warning);

            /* Access denied */

            var acl = File.GetAccessControl(outputFile);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.Read, AccessControlType.Deny));
            File.SetAccessControl(outputFile, acl);

            ((ReadDebugDataStep)steps[0]).OutputFile.CheckTimestamp = false;
            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Debug data is missing. Access is denied to local file at {outputFile}", result.StepResults[0].Warning);

            /* File not found */

            File.Delete(outputFile);
            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Debug data is missing. File is not found on the local machine at {outputFile}", result.StepResults[0].Warning);

            /* Wrong output file size */

            File.WriteAllText(watchesFile, File.ReadAllText(TestHelper.GetFixturePath("ValidWatches.txt")));
            File.WriteAllText(dispatchFile, File.ReadAllText(TestHelper.GetFixturePath("DispatchParams.txt")));
            File.WriteAllBytes(outputFile, new byte[1024]);
            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal(@"Debug data is invalid. Output file does not match the expected size.

Grid size as specified in the dispatch parameters file is (16384, 1, 1), or 16384 lanes in total. With 7 DWORDs per lane, the output file is expected to be 114688 DWORDs long, but the actual size is 256 DWORDs.",
result.StepResults[0].Warning);
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
            readDebugData.DispatchParamsFile.Location = StepEnvironment.Remote;
            readDebugData.DispatchParamsFile.CheckTimestamp = false;
            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, IfNotModified = ActionIfNotModified.Fail, SourcePath = "/home/parker/audio/checked", TargetPath = Path.GetTempFileName() },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, IfNotModified = ActionIfNotModified.Copy, SourcePath = "/home/parker/audio/unchecked", TargetPath = Path.GetTempFileName() },
                readDebugData
            };

            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(), _project);

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
