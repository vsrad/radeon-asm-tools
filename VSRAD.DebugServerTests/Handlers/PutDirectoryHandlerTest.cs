using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class PutDirectoryHandlerTest
    {
        [Fact]
        public async Task SuccessTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var files = new[]
            {
                new PackedFile(new byte[] { 0x4C }, "test", new DateTime(1998, 07, 06, 0, 0, 0, DateTimeKind.Utc)),
                new PackedFile(new byte[] { 0x4C, 0x41, 0x49 }, "dir/test", new DateTime(1998, 07, 13, 0, 0, 0, DateTimeKind.Utc)),
                new PackedFile(new byte[] { 0x4E }, "nested/dir/test", new DateTime(1998, 07, 20, 0, 0, 0, DateTimeKind.Utc)),
                new PackedFile(Array.Empty<byte>(), "empty/dir/", new DateTime(1998, 07, 27, 0, 0, 0, DateTimeKind.Utc)),
            };

            var response = await Helper.DispatchCommandAsync<PutDirectoryCommand, PutDirectoryResponse>(new PutDirectoryCommand
            {
                Files = files,
                Path = Path.GetFileName(tmpPath),
                WorkDir = Path.GetDirectoryName(tmpPath),
                PreserveTimestamps = true
            });

            Assert.Equal(PutDirectoryStatus.Successful, response.Status);

            Assert.True(File.Exists(Path.Combine(tmpPath, "test")));
            Assert.Equal(new byte[] { 0x4C }, File.ReadAllBytes(Path.Combine(tmpPath, "test")));
            Assert.Equal(new DateTime(1998, 07, 06), File.GetLastWriteTimeUtc(Path.Combine(tmpPath, "test")));

            Assert.True(File.Exists(Path.Combine(tmpPath, "dir", "test")));
            Assert.Equal(new byte[] { 0x4C, 0x41, 0x49 }, File.ReadAllBytes(Path.Combine(tmpPath, "dir", "test")));
            Assert.Equal(new DateTime(1998, 07, 13), File.GetLastWriteTimeUtc(Path.Combine(tmpPath, "dir", "test")));

            Assert.True(File.Exists(Path.Combine(tmpPath, "nested", "dir", "test")));
            Assert.Equal(new byte[] { 0x4E }, File.ReadAllBytes(Path.Combine(tmpPath, "nested", "dir", "test")));
            Assert.Equal(new DateTime(1998, 07, 20), File.GetLastWriteTimeUtc(Path.Combine(tmpPath, "nested", "dir", "test")));

            Assert.True(Directory.Exists(Path.Combine(tmpPath, "empty", "dir")));
            Assert.Equal(new DateTime(1998, 07, 27), Directory.GetLastWriteTimeUtc(Path.Combine(tmpPath, "empty", "dir")));

            Directory.Delete(tmpPath, recursive: true);
        }

        [Fact]
        public async Task TargetPathIsFileTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllText(tmpPath, "existing file");

            var response = await Helper.DispatchCommandAsync<PutDirectoryCommand, PutDirectoryResponse>(new PutDirectoryCommand
            {
                Files = Array.Empty<PackedFile>(),
                Path = Path.GetFileName(tmpPath),
                WorkDir = Path.GetDirectoryName(tmpPath)
            });

            Assert.Equal(PutDirectoryStatus.TargetPathIsFile, response.Status);
            Assert.Equal("existing file", File.ReadAllText(tmpPath));

            File.Delete(tmpPath);
        }

        [Fact]
        public async Task PermissionDeniedTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpPath);
            File.WriteAllText(Path.Combine(tmpPath, "test"), "read only");
            File.SetAttributes(Path.Combine(tmpPath, "test"), FileAttributes.ReadOnly);

            var files = new[] { new PackedFile(new byte[] { 0x48 }, "test", new DateTime(2002, 10, 09, 0, 0, 0, DateTimeKind.Utc)) };

            var response = await Helper.DispatchCommandAsync<PutDirectoryCommand, PutDirectoryResponse>(new PutDirectoryCommand
            {
                Files = files,
                Path = tmpPath,
                WorkDir = ""
            });

            Assert.Equal(PutDirectoryStatus.PermissionDenied, response.Status);

            File.SetAttributes(Path.Combine(tmpPath, "test"), FileAttributes.Normal);
            Directory.Delete(tmpPath, recursive: true);
        }
    }
}
