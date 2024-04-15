using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class GetFilesHandlerTest
    {
        [Fact]
        public async Task PartialRequestTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpPath + "\\k");    // .\k\
            Directory.CreateDirectory(tmpPath + "\\h\\b"); // .\h\b\
            Directory.CreateDirectory(tmpPath + "\\empty"); // .\empty

            File.WriteAllBytes(tmpPath + "\\t", Array.Empty<byte>()); // not requested
            File.WriteAllBytes(tmpPath + "\\k\\s", new byte[] { 0x63, 0x72, 0x6f, 0x77 });          // .\k\s
            File.WriteAllBytes(tmpPath + "\\h\\b\\w", new byte[] { 0x74, 0x65, 0x6f, 0x74, 0x77 }); // .\h\b\w

            File.SetLastWriteTimeUtc(tmpPath + "\\k\\s", new DateTime(2002, 09, 12));
            File.SetLastWriteTimeUtc(tmpPath + "\\h\\b\\w", new DateTime(1985, 06, 15));
            Directory.SetLastWriteTimeUtc(tmpPath + "\\empty", new DateTime(1981, 01, 01));

            var response = await Helper.DispatchCommandAsync<GetFilesCommand, GetFilesResponse>(new GetFilesCommand
            {
                UseCompression = false,
                RootPath = tmpPath,
                Paths = new[] { "k/s", "h/b/w", "empty/" },
            });

            Assert.Equal(GetFilesStatus.Successful, response.Status);

            Assert.Equal(3, response.Files.Length);

            Assert.Equal("k/s", response.Files[0].RelativePath);
            Assert.Equal(new byte[] { 0x63, 0x72, 0x6f, 0x77 }, response.Files[0].Data);
            Assert.Equal(new DateTime(2002, 09, 12, 0, 0, 0, DateTimeKind.Utc), response.Files[0].LastWriteTimeUtc);

            Assert.Equal("h/b/w", response.Files[1].RelativePath);
            Assert.Equal(new byte[] { 0x74, 0x65, 0x6f, 0x74, 0x77 }, response.Files[1].Data);
            Assert.Equal(new DateTime(1985, 06, 15, 0, 0, 0, DateTimeKind.Utc), response.Files[1].LastWriteTimeUtc);

            Assert.Equal("empty/", response.Files[2].RelativePath);
            Assert.Equal(Array.Empty<byte>(), response.Files[2].Data);
            Assert.Equal(new DateTime(1981, 01, 01, 0, 0, 0, DateTimeKind.Utc), response.Files[2].LastWriteTimeUtc);

            Directory.Delete(tmpPath, recursive: true);
        }

        [Fact]
        public async Task FileDoesNotExistTest()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            // Directory does not exist
            var response = await Helper.DispatchCommandAsync<GetFilesCommand, GetFilesResponse>(new GetFilesCommand
            {
                UseCompression = true,
                RootPath = tmpPath,
                Paths = new[] { "test" }
            });
            Assert.Equal(GetFilesStatus.FileOrDirectoryNotFound, response.Status);

            Directory.CreateDirectory(tmpPath);

            // File does not exist
            response = await Helper.DispatchCommandAsync<GetFilesCommand, GetFilesResponse>(new GetFilesCommand
            {
                UseCompression = true,
                RootPath = tmpPath,
                Paths = new[] { "test" }
            });
            Assert.Equal(GetFilesStatus.FileOrDirectoryNotFound, response.Status);
        }
    }
}
