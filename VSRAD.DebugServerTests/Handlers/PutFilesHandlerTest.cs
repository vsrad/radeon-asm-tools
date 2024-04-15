using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class PutFilesHandlerTest
    {
        [Fact]
        public async Task SuccessTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var files = new[]
            {
                new PackedFile("test", new DateTime(1998, 07, 06, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0x4C }),
                new PackedFile("dir/test", new DateTime(1998, 07, 13, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0x4C, 0x41, 0x49 }),
                new PackedFile("nested/dir/test", new DateTime(1998, 07, 20, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0x4E }),
                new PackedFile("empty/dir/", new DateTime(1998, 07, 27, 0, 0, 0, DateTimeKind.Utc), Array.Empty<byte>()),
            };

            var response = await Helper.DispatchCommandAsync<PutFilesCommand, PutFilesResponse>(new PutFilesCommand
            {
                Files = files,
                RootPath = tmpPath,
                PreserveTimestamps = true
            });

            Assert.Equal(PutFilesStatus.Successful, response.Status);

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
        public async Task SingleFileTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllText(tmpPath, "existing file");

            var response = await Helper.DispatchCommandAsync<PutFilesCommand, PutFilesResponse>(new PutFilesCommand
            {
                Files = new[] { new PackedFile("", default, Encoding.UTF8.GetBytes("copied file")) },
                RootPath = tmpPath
            });

            Assert.Equal(PutFilesStatus.Successful, response.Status);
            Assert.Equal("copied file", File.ReadAllText(tmpPath));

            File.Delete(tmpPath);
        }

        [Fact]
        public async Task PermissionDeniedTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpPath);
            File.WriteAllText(Path.Combine(tmpPath, "test"), "read only");
            File.SetAttributes(Path.Combine(tmpPath, "test"), FileAttributes.ReadOnly);

            var files = new[] { new PackedFile("test", new DateTime(2002, 10, 09, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0x48 }) };

            var response = await Helper.DispatchCommandAsync<PutFilesCommand, PutFilesResponse>(new PutFilesCommand
            {
                Files = files,
                RootPath = tmpPath
            });

            Assert.Equal(PutFilesStatus.PermissionDenied, response.Status);

            File.SetAttributes(Path.Combine(tmpPath, "test"), FileAttributes.Normal);
            Directory.Delete(tmpPath, recursive: true);
        }

        [Fact]
        public async Task IOErrorTestAsync()
        {
            var tmpPath = Path.GetTempFileName();

            using (var fs = new FileStream(tmpPath, FileMode.Open))
            {
                var files = new[] { new PackedFile(Path.GetFileName(tmpPath), new DateTime(2004, 04, 11, 0, 0, 0, DateTimeKind.Utc), new byte[] { 0x48 }) };
                var response = await Helper.DispatchCommandAsync<PutFilesCommand, PutFilesResponse>(new PutFilesCommand
                {
                    Files = files,
                    RootPath = Path.GetDirectoryName(tmpPath)
                });
                Assert.Equal(PutFilesStatus.OtherIOError, response.Status);
            }

            File.Delete(tmpPath);
        }
    }
}
