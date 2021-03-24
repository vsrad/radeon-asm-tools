using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
            Directory.SetLastWriteTimeUtc(tmpPath + "\\empty", new DateTime(1980, 01, 01));

            var response = await Helper.DispatchCommandAsync<GetFilesCommand, GetFilesResponse>(new GetFilesCommand
            {
                UseCompression = true,
                Paths = new[] { "k/s", "h/b/w", "empty/" },
                RootPath = new[] { tmpPath }
            });

            Assert.Equal(GetFilesStatus.Successful, response.Status);

            var items = ReadZipItems(response.ZipData).ToArray();
            Assert.Equal(3, items.Length);

            Assert.Equal("k/s", items[0].Path);
            Assert.Equal(new byte[] { 0x63, 0x72, 0x6f, 0x77 }, items[0].Data);
            Assert.Equal(new DateTime(2002, 09, 12), items[0].LastWriteTime);

            Assert.Equal("h/b/w", items[1].Path);
            Assert.Equal(new byte[] { 0x74, 0x65, 0x6f, 0x74, 0x77 }, items[1].Data);
            Assert.Equal(new DateTime(1985, 06, 15), items[1].LastWriteTime);

            Assert.Equal("empty/", items[2].Path);
            Assert.Equal(Array.Empty<byte>(), items[2].Data);
            Assert.Equal(new DateTime(1980, 01, 01), items[2].LastWriteTime);

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
                Paths = new[] { "test" },
                RootPath = new[] { tmpPath }
            });
            Assert.Equal(GetFilesStatus.FileNotFound, response.Status);

            Directory.CreateDirectory(tmpPath);

            // File does not exist
            response = await Helper.DispatchCommandAsync<GetFilesCommand, GetFilesResponse>(new GetFilesCommand
            {
                UseCompression = true,
                Paths = new[] { "test" },
                RootPath = new[] { tmpPath }
            });
            Assert.Equal(GetFilesStatus.FileNotFound, response.Status);
        }

        private static IEnumerable<(string Path, byte[] Data, DateTime LastWriteTime)> ReadZipItems(byte[] zipBytes)
        {
            using var stream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(stream);

            foreach (var e in archive.Entries)
            {
                using var s = new MemoryStream();
                using var dataStream = e.Open();
                dataStream.CopyTo(s);
                yield return (e.FullName, s.ToArray(), e.LastWriteTime.DateTime);
            }
        }
    }
}
