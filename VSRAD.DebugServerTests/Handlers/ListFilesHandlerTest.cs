using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class ListFilesHandlerTest
    {
        [Fact]
        public async Task FileTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllText(tmpPath, "12345"); // .
            File.SetLastWriteTimeUtc(tmpPath, DateTime.FromFileTimeUtc(1));

            var response = await Helper.DispatchCommandAsync<ListFilesCommand, ListFilesResponse>(new ListFilesCommand
            {
                Path = Path.GetFileName(tmpPath),
                WorkDir = Path.GetDirectoryName(tmpPath)
            });

            Assert.Single(response.Files);
            Assert.Equal((".", false, 5, DateTime.FromFileTimeUtc(1)), response.Files[0]);

            File.Delete(tmpPath);
        }

        [Fact]
        public async Task DirectoryHierarchyTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpPath);            // .
            Directory.CreateDirectory(tmpPath + "\\a");    // .\a\
            Directory.CreateDirectory(tmpPath + "\\b\\c"); // .\b\c\

            File.WriteAllText(tmpPath + "\\t", "1234");    // .\t
            File.WriteAllText(tmpPath + "\\a\\t", "123");  // .\a\t
            File.WriteAllText(tmpPath + "\\b\\c\\t", "1"); // .\b\c\t

            Directory.SetLastWriteTimeUtc(tmpPath, DateTime.FromFileTimeUtc(1));
            Directory.SetLastWriteTimeUtc(tmpPath + "\\a", DateTime.FromFileTimeUtc(2));
            Directory.SetLastWriteTimeUtc(tmpPath + "\\b", DateTime.FromFileTimeUtc(3));
            File.SetLastWriteTimeUtc(tmpPath + "\\t", DateTime.FromFileTimeUtc(4));
            File.SetLastWriteTimeUtc(tmpPath + "\\a\\t", DateTime.FromFileTimeUtc(5));
            Directory.SetLastWriteTimeUtc(tmpPath + "\\b\\c", DateTime.FromFileTimeUtc(6));
            File.SetLastWriteTimeUtc(tmpPath + "\\b\\c\\t", DateTime.FromFileTimeUtc(7));

            var response = await Helper.DispatchCommandAsync<ListFilesCommand, ListFilesResponse>(new ListFilesCommand
            {
                Path = tmpPath,
                WorkDir = ""
            });

            Assert.Equal(7, response.Files.Length);
            Assert.Equal((".", true, 0, DateTime.FromFileTimeUtc(1)), response.Files[0]);
            Assert.Equal(("a", true, 0, DateTime.FromFileTimeUtc(2)), response.Files[1]);
            Assert.Equal(("b", true, 0, DateTime.FromFileTimeUtc(3)), response.Files[2]);
            Assert.Equal(("t", false, 4, DateTime.FromFileTimeUtc(4)), response.Files[3]);
            Assert.Equal(("a\\t", false, 3, DateTime.FromFileTimeUtc(5)), response.Files[4]);
            Assert.Equal(("b\\c", true, 0, DateTime.FromFileTimeUtc(6)), response.Files[5]);
            Assert.Equal(("b\\c\\t", false, 1, DateTime.FromFileTimeUtc(7)), response.Files[6]);

            Directory.Delete(tmpPath, recursive: true);
        }

        [Fact]
        public async Task FileNotFoundTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Assert.False(File.Exists(tmpPath));

            var response = await Helper.DispatchCommandAsync<ListFilesCommand, ListFilesResponse>(new ListFilesCommand
            {
                Path = Path.GetFileName(tmpPath),
                WorkDir = Path.GetDirectoryName(tmpPath)
            });

            Assert.Empty(response.Files);
        }
    }
}
