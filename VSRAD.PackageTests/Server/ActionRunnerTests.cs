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
        private readonly IActionRunnerCallbacks _mockCallbacks = new Mock<IActionRunnerCallbacks>().Object;

        [Fact]
        public async Task SucessfulRunTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" },
                new VerifyFileModifiedStep { Location = StepEnvironment.Remote, AbortIfNotModifed = true, Path = "/home/mizu/machete/tweened.tvpp" },
                new CopyStep { Direction = CopyDirection.RemoteToLocal, SourcePath = "/home/mizu/machete/tweened.tvpp", TargetPath = tmpFile },
                new VerifyFileModifiedStep { Location = StepEnvironment.Local, AbortIfNotModifed = true, Path = tmpFile },
            };
            var localTempFile = Path.GetRandomFileName();
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) }, (FetchMetadata command) =>
            {
                // init timestamp fetch
                Assert.Equal("/home/mizu/machete/tweened.tvpp", command.FilePath);
            });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "", Stderr = "" });
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(101) });
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata("", 1, DateTime.FromBinary(101)) } });
            channel.ThenRespond(new GetFilesResponse { Status = GetFilesStatus.Successful, Files = new[] { new PackedFile("", DateTime.FromBinary(101), Encoding.UTF8.GetBytes("file-contents")) } });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);
            Assert.True(result.StepResults[0].Successful);
            Assert.Equal("", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (exit code 0)\r\n", result.StepResults[0].Log);
            Assert.True(result.StepResults[1].Successful);
            Assert.Equal("", result.StepResults[1].Warning);
            Assert.Equal("", result.StepResults[1].Log);
            Assert.True(result.StepResults[2].Successful);
            Assert.Equal("", result.StepResults[2].Warning);
            Assert.Equal("", result.StepResults[2].Log);
            Assert.Equal("file-contents", File.ReadAllText(tmpFile));
            File.Delete(tmpFile);
        }

        #region CopyStep
        [Fact]
        public async Task CopyFileRLRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());
            var steps = new List<IActionStep>
            {
                new CopyStep { Direction = CopyDirection.RemoteToLocal, SkipIfNotModified = false, SourcePath = "/home/mizu/machete/key3_49", TargetPath = Path.GetRandomFileName() },
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" } // should not be run
            };

            channel.ThenRespond(new ListFilesResponse { Files = Array.Empty<FileMetadata>() });
            var result = await runner.RunAsync("HTMT", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("The source file or directory is missing on the remote machine at /home/mizu/machete/key3_49", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyFileRLMissingParentDirectoryTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());

            var parentDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Assert.False(Directory.Exists(parentDir));

            var file = Path.Combine(parentDir, "local-copy");
            var steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.RemoteToLocal, SourcePath = "/home/mizu/machete/raw3", TargetPath = file } };

            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata("", 1, default) } });
            channel.ThenRespond(new GetFilesResponse { Status = GetFilesStatus.Successful, Files = new[] { new PackedFile("", default, Encoding.UTF8.GetBytes("file-contents")) } });

            var result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);
            Assert.Equal("file-contents", File.ReadAllText(file));
            File.Delete(file);
        }

        [Fact]
        public async Task CopyFileRLLocalErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());

            var file = Path.GetTempFileName();
            File.SetAttributes(file, FileAttributes.ReadOnly);
            var steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.RemoteToLocal, SourcePath = "/home/mizu/machete/raw3", TargetPath = file } };
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata("", default, default) } });
            channel.ThenRespond(new GetFilesResponse { Status = GetFilesStatus.Successful, Files = new[] { new PackedFile("", default, Encoding.UTF8.GetBytes("file-contents")) } });

            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Failed to access the local target path. Access to the path '{file}' is denied. Make sure that the path is not marked as read-only.", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyFileLRRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());
            var steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.LocalToRemote, SourcePath = Path.GetTempFileName(), TargetPath = "/home/mizu/machete/raw3" } };

            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata("", default, default) } });
            channel.ThenRespond(new PutFilesResponse { Status = PutFilesStatus.PermissionDenied, ErrorMessage = $"Access to the path {((CopyStep)steps[0]).TargetPath} is denied." });
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal("Failed to access the remote target path. Access to the path /home/mizu/machete/raw3 is denied. Make sure that the path is not marked as read-only.", result.StepResults[0].Warning);

            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata("", default, default) } });
            channel.ThenRespond(new PutFilesResponse { Status = PutFilesStatus.OtherIOError, ErrorMessage = "Some IO error." });
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal("Failed to write file(s) to the remote target path. Some IO error.", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyFileLLErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());

            var nonexistentPath = @"C:\Non\Existent\Path\To\Users\mizu\raw3";
            var steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.LocalToLocal, SourcePath = nonexistentPath, TargetPath = Path.GetRandomFileName() } };
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"The source file or directory is missing on the local machine at {nonexistentPath}", result.StepResults[0].Warning);

            var lockedPath = Path.GetTempFileName();
            var acl = File.GetAccessControl(lockedPath);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.Read, AccessControlType.Deny));
            File.SetAccessControl(lockedPath, acl);
            steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.LocalToLocal, SourcePath = lockedPath, TargetPath = Path.GetRandomFileName() } };
            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Failed to access the local source path. Access to the path '{lockedPath}' is denied.", result.StepResults[0].Warning);
            File.Delete(lockedPath);
        }

        [Fact]
        public async Task CopyFileLLTestAsync()
        {
            var runner = new ActionRunner(channel: null, _mockCallbacks, new ActionEnvironment());

            var file = Path.GetTempFileName();
            var target = Path.GetTempFileName();
            File.WriteAllText(file, "local to local copy test");
            var steps = new List<IActionStep>
            {
                // update file timestamp
                new ExecuteStep { Environment = StepEnvironment.Local, Executable = "cmd.exe", Arguments = $@"/C ""copy /b {Path.GetFileName(file)} +,,""", WorkingDirectory = Path.GetDirectoryName(file) },
                new CopyStep { Direction = CopyDirection.LocalToLocal, SourcePath = file, TargetPath = target, SkipIfNotModified = false }
            };
            var result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);
            Assert.Equal("local to local copy test", File.ReadAllText(target));

            // In case of redundant local-to-local copies, no files are accessed
            var localPath = @"C:\Non\Existent\Path\To\Some\Local\File\Or\Directory";
            steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.LocalToLocal, SourcePath = localPath, TargetPath = localPath, SkipIfNotModified = false } };
            result = await runner.RunAsync("HTMT", steps);
            Assert.True(result.Successful);
            Assert.Equal($"Copy skipped. The source and target locations are identical.\r\n", result.StepResults[0].Log);
        }

        [Fact]
        public async Task CopyDirectoryLRTestAsync()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpDir);
            Directory.CreateDirectory(tmpDir + "\\dir");
            Directory.SetLastWriteTimeUtc(tmpDir + "\\dir", new DateTime(1970, 1, 1));
            File.WriteAllText(tmpDir + "\\t", "test");
            File.WriteAllText(tmpDir + "\\dir\\t2", "test2");
            File.SetLastWriteTimeUtc(tmpDir + "\\t", new DateTime(1980, 1, 1));
            File.SetLastWriteTimeUtc(tmpDir + "\\dir\\t2", new DateTime(1990, 1, 1));

            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());
            var steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.LocalToRemote, SourcePath = tmpDir, TargetPath = "/home/mizu/rawdir", SkipIfNotModified = true } };

            // t is unchanged, dir/ is missing
            channel.ThenRespond(new ListFilesResponse
            {
                Files = new[]
                {
                    new FileMetadata("./", default, default),
                    new FileMetadata("t", 4, new DateTime(1980, 1, 1))
                }
            }, (ListFilesCommand command) =>
            {
                Assert.Equal("/home/mizu/rawdir", command.RootPath);
            });
            channel.ThenRespond(new PutFilesResponse { Status = PutFilesStatus.Successful }, (PutFilesCommand command) =>
            {
                Assert.Equal(2, command.Files.Length);
                Assert.Equal("dir/", command.Files[0].RelativePath);
                Assert.Equal(Array.Empty<byte>(), command.Files[0].Data);
                Assert.Equal(new DateTime(1970, 1, 1), command.Files[0].LastWriteTimeUtc);
                Assert.Equal("dir/t2", command.Files[1].RelativePath);
                Assert.Equal("test2", Encoding.UTF8.GetString(command.Files[1].Data));
                Assert.Equal(new DateTime(1990, 1, 1), command.Files[0].LastWriteTimeUtc);
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
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());
            var steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.LocalToRemote, SourcePath = tmpDir, TargetPath = "/home/mizu/rawdir", SkipIfNotModified = true } };

            // Path does not exist
            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"The source file or directory is missing on the local machine at {tmpDir}", result.StepResults[0].Warning);

            // Access denied to root path
            var denyListDirectoryRule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.ListDirectory, AccessControlType.Deny);
            var root = Directory.CreateDirectory(tmpDir);
            var rootAcl = root.GetAccessControl();
            rootAcl.AddAccessRule(denyListDirectoryRule);
            root.SetAccessControl(rootAcl);

            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Failed to access local path {root.FullName}. Access to the path '{root.FullName}' is denied.", result.StepResults[0].Warning);

            rootAcl.RemoveAccessRule(denyListDirectoryRule);
            root.SetAccessControl(rootAcl);

            // Access denied to a subdirectory
            var subdir = root.CreateSubdirectory("subdir");
            var subdirAcl = subdir.GetAccessControl();
            subdirAcl.AddAccessRule(denyListDirectoryRule);
            subdir.SetAccessControl(subdirAcl);

            result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($"Failed to access local path {root.FullName}. Access to the path '{subdir.FullName}' is denied.", result.StepResults[0].Warning);

            subdirAcl.RemoveAccessRule(denyListDirectoryRule);
            subdir.SetAccessControl(subdirAcl);

            root.Delete(recursive: true);
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
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());
            var steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.RemoteToLocal, SourcePath = "/home/mizu/rawdir", TargetPath = tmpDir, SkipIfNotModified = true } };

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
                Assert.Equal("/home/mizu/rawdir", command.RootPath);
            });

            var files = new[] { new PackedFile("t2", new DateTime(1990, 1, 1), new byte[] { 0, 1, 2, 3 }) };
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
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());
            var steps = new List<IActionStep> { new CopyStep { Direction = CopyDirection.RemoteToLocal, SourcePath = "/home/mizu/rawdir", TargetPath = tmpDir, SkipIfNotModified = true } };

            // t's size is changed => it'll be requested
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata("./", default, default), new FileMetadata("t", 1, default) } });
            var files = new[] { new PackedFile("t", new DateTime(1990, 1, 1), new byte[] { 0, 1, 2, 3 }) };
            channel.ThenRespond(new GetFilesResponse { Status = GetFilesStatus.Successful, Files = files });

            // Permission denied
            File.SetAttributes(tmpDir + "\\t", FileAttributes.ReadOnly);

            var result = await runner.RunAsync("HTMT", steps);
            Assert.False(result.Successful);
            Assert.Equal($@"Failed to access the local target path. Access to the path '{tmpDir}\t' is denied. Make sure that the path is not marked as read-only.", result.StepResults[0].Warning);

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
                new CopyStep { Direction = CopyDirection.RemoteToLocal, SkipIfNotModified = false, TargetPath = "/home/parker/audio/unchecked", SourcePath = "" }, // should not be run
            };
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch, Stdout = "", Stderr = "" });
            var result = await runner.RunAsync("UFOW", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Check that the executable is specified correctly. (The remote `dvd-prepare` process could not be started.)", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (could not launch)\r\n", result.StepResults[0].Log);

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.TimedOut, Stdout = "...\n", Stderr = "Could not prepare master DVD, deadline exceeded.\n\n" });
            result = await runner.RunAsync("UFOW", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Execution timeout is exceeded. (The remote `dvd-prepare` process is terminated.)", result.StepResults[0].Warning);
            Assert.Equal("Captured stdout (timed out):\r\n...\r\nCaptured stderr (timed out):\r\nCould not prepare master DVD, deadline exceeded.\r\n", result.StepResults[0].Log);

            /* Non-zero exit code results in a failed run with an error */
            steps = new List<IActionStep> { new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "dvd-prepare" } };
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 1, Stdout = "", Stderr = "Looks like you fell asleep ¯\\_(ツ)_/¯\n\n" });
            result = await runner.RunAsync("UFOW", steps, false);
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Check the errors in Error List and the command output in Output -> RAD Debug. (The remote `dvd-prepare` process exited with non-zero code 1.)", result.StepResults[0].Warning);
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
            var runner = new ActionRunner(channel: null, _mockCallbacks, new ActionEnvironment());
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
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());

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
                    outputFile: new BuiltinActionFile { Location = StepEnvironment.Remote, Path = "output", CheckTimestamp = true },
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
                new CopyStep { Direction = CopyDirection.RemoteToLocal, SkipIfNotModified = false, SourcePath = "/home/mizu/machete/tweened.tvpp", TargetPath = Path.GetTempFileName() }
            };
            // 1. Level 2 Execute
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "level2", Stderr = "" }, (Execute command) =>
            {
                Assert.Equal("autotween", command.Executable);
            });
            // 2. Level 3 timestamp fetch
            // 3. Level 3 Execute
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "level3", Stderr = "" }, (Execute command) =>
            {
                Assert.Equal("cleanup", command.Executable);
            });
            // 4. Level 3 Read Debug Data
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(101), Data = TestHelper.ReadFixtureBytes("ValidWatches.txt") });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("DispatchParams.txt") });
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, ByteCount = TestHelper.GetFixtureSize("DebugBuffer.bin") });
            // 4. Level 1 Copy
            channel.ThenRespond(new ListFilesResponse { Files = new[] { new FileMetadata("", default, default) } });
            channel.ThenRespond(new GetFilesResponse { Status = GetFilesStatus.Successful, Files = new[] { new PackedFile("", default, Encoding.UTF8.GetBytes("file-contents")) } });
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")));
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
            var runner = new ActionRunner(channel: null, _mockCallbacks, new ActionEnvironment(watches: watches, breakTarget: breakTarget));
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
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")));

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata initTimestampFetch) =>
                Assert.Equal("/glitch/city/output", initTimestampFetch.FilePath));
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("ValidWatches.txt") }, (FetchResultRange watchesFetch) =>
                Assert.Equal("/glitch/city/watches", watchesFetch.FilePath));
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("DispatchParams.txt") }, (FetchResultRange statusFetch) =>
                Assert.Equal("/glitch/city/status", statusFetch.FilePath));
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, ByteCount = TestHelper.GetFixtureSize("DebugBuffer.bin") }, (FetchMetadata outputMetaFetch) =>
                Assert.Equal("/glitch/city/output", outputMetaFetch.FilePath));

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
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")));

            /* File not found */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(i) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.FileNotFound }, (FetchResultRange w) => Assert.Equal("/remote/watches", w.FilePath));

            var result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Valid watches data is missing. File could not be found on the remote machine at /remote/watches", result.StepResults[0].Warning);

            /* File not changed */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(i) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(0) },
                (FetchResultRange w) => Assert.Equal("/remote/watches", w.FilePath));

            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Valid watches data is stale. File was not modified by the debug action on the remote machine at /remote/watches", result.StepResults[0].Warning);

            /* Wrong output file size */

            for (int i = 0; i < 3; ++i) // initial timestamp fetch
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(0) });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, Data = TestHelper.ReadFixtureBytes("ValidWatches.txt") },
                (FetchResultRange f) => Assert.Equal("/remote/watches", f.FilePath));
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, Data = TestHelper.ReadFixtureBytes("DispatchParams.txt") },
                (FetchResultRange f) => Assert.Equal("/remote/dispatch", f.FilePath));
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, ByteCount = 65536 },
                (FetchMetadata f) => Assert.Equal("/remote/output", f.FilePath));

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
            var runner = new ActionRunner(channel: null, _mockCallbacks, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")));
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
            var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment(watches: TestHelper.ReadFixtureLines("Watches.txt")));

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
            Assert.Equal($"Debug data is missing. Access denied. Failed to read local file {outputFile}", result.StepResults[0].Warning);

            /* File not found */

            File.Delete(outputFile);
            result = await runner.RunAsync("Debug", steps);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal($"Debug data is missing. File not found. Failed to read local file {outputFile}", result.StepResults[0].Warning);

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
        public async Task VerifyFileModifiedLocalTestAsync()
        {
            var testFile = Path.GetTempFileName();
            /* File is modified */
            {
                var steps = new List<IActionStep>
                {
                    new ExecuteStep { Environment = StepEnvironment.Local, Executable = "cmd.exe", Arguments = $@"/C ""copy /b {Path.GetFileName(testFile)} +,,""", WorkingDirectory = Path.GetDirectoryName(testFile) },
                    new VerifyFileModifiedStep { Path = testFile, AbortIfNotModifed = true, ErrorMessage = "Custom file not modifed message" }
                };
                var runner = new ActionRunner(channel: null, _mockCallbacks, new ActionEnvironment());
                var result = await runner.RunAsync("", steps);
                Assert.True(result.Successful);
                Assert.Empty(result.StepResults[0].Warning);
            }
            /* File is not modified (error) */
            {
                var steps = new List<IActionStep> { new VerifyFileModifiedStep { Path = testFile, AbortIfNotModifed = true, ErrorMessage = "Custom file not modifed message" } };
                var runner = new ActionRunner(channel: null, _mockCallbacks, new ActionEnvironment());
                var result = await runner.RunAsync("", steps);
                Assert.False(result.Successful);
                Assert.Equal("Custom file not modifed message", result.StepResults[0].Warning);
            }
            /* File is not modified (warning) */
            {
                var steps = new List<IActionStep> { new VerifyFileModifiedStep { Path = testFile, AbortIfNotModifed = false, ErrorMessage = "Custom file not modifed message" } };
                var runner = new ActionRunner(channel: null, _mockCallbacks, new ActionEnvironment());
                var result = await runner.RunAsync("", steps);
                Assert.True(result.Successful);
                Assert.Equal("Custom file not modifed message", result.StepResults[0].Warning);
            }
            File.Delete(testFile);
        }

        [Fact]
        public async Task VerifyFileModifiedRemoteTestAsync()
        {
            /* File is modified */
            {
                var channel = new MockCommunicationChannel();
                var steps = new List<IActionStep> { new VerifyFileModifiedStep { Location = StepEnvironment.Remote, Path = "/test/file", AbortIfNotModifed = true, ErrorMessage = "Custom file not modifed message" } };
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(0) }, (FetchMetadata command) =>
                    Assert.Equal("/test/file", command.FilePath));
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(1) }, (FetchMetadata command) =>
                    Assert.Equal("/test/file", command.FilePath));
                var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());
                var result = await runner.RunAsync("", steps);
                Assert.True(result.Successful);
                Assert.Empty(result.StepResults[0].Warning);
            }
            /* File is not modified (error) */
            {
                var channel = new MockCommunicationChannel();
                var steps = new List<IActionStep> { new VerifyFileModifiedStep { Location = StepEnvironment.Remote, Path = "/test/file", AbortIfNotModifed = true, ErrorMessage = "Custom file not modifed message" } };
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(0) }, (FetchMetadata command) =>
                    Assert.Equal("/test/file", command.FilePath));
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(0) }, (FetchMetadata command) =>
                    Assert.Equal("/test/file", command.FilePath));
                var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());
                var result = await runner.RunAsync("", steps);
                Assert.False(result.Successful);
                Assert.Equal("Custom file not modifed message", result.StepResults[0].Warning);
            }
            /* File is not modified (warning) */
            {
                var channel = new MockCommunicationChannel();
                var steps = new List<IActionStep> { new VerifyFileModifiedStep { Location = StepEnvironment.Remote, Path = "/test/file", AbortIfNotModifed = false, ErrorMessage = "Custom file not modifed message" } };
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata command) =>
                    Assert.Equal("/test/file", command.FilePath));
                channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata command) =>
                    Assert.Equal("/test/file", command.FilePath));
                var runner = new ActionRunner(channel.Object, _mockCallbacks, new ActionEnvironment());
                var result = await runner.RunAsync("", steps);
                Assert.True(result.Successful);
                Assert.Equal("Custom file not modifed message", result.StepResults[0].Warning);
            }
        }
    }
}
