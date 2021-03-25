using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.DebugServer.SharedUtils;
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
                WorkDir = Path.GetDirectoryName(tmpPath),
                IncludeSubdirectories = true
            });

            Assert.Single(response.Files);
            Assert.Equal(new FileMetadata(".", 5, DateTime.FromFileTimeUtc(1)), response.Files[0]);

            File.Delete(tmpPath);
        }

        [Fact]
        public async Task IncludeSubdirectoriesTestAsync()
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
                WorkDir = "",
                IncludeSubdirectories = true
            });

            Assert.Equal(7, response.Files.Length);
            Assert.Equal(new FileMetadata("./", 0, DateTime.FromFileTimeUtc(1)), response.Files[0]);
            Assert.True(response.Files[0].IsDirectory);
            Assert.Equal(new FileMetadata("a/", 0, DateTime.FromFileTimeUtc(2)), response.Files[1]);
            Assert.True(response.Files[1].IsDirectory);
            Assert.Equal(new FileMetadata("b/", 0, DateTime.FromFileTimeUtc(3)), response.Files[2]);
            Assert.True(response.Files[2].IsDirectory);
            Assert.Equal(new FileMetadata("t", 4, DateTime.FromFileTimeUtc(4)), response.Files[3]);
            Assert.False(response.Files[3].IsDirectory);
            Assert.Equal(new FileMetadata("a/t", 3, DateTime.FromFileTimeUtc(5)), response.Files[4]);
            Assert.False(response.Files[4].IsDirectory);
            Assert.Equal(new FileMetadata("b/c/", 0, DateTime.FromFileTimeUtc(6)), response.Files[5]);
            Assert.True(response.Files[5].IsDirectory);
            Assert.Equal(new FileMetadata("b/c/t", 1, DateTime.FromFileTimeUtc(7)), response.Files[6]);
            Assert.False(response.Files[6].IsDirectory);

            Directory.Delete(tmpPath, recursive: true);
        }

        [Fact]
        public async Task ExcludeSubdirectoriesTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpPath);            // .
            Directory.CreateDirectory(tmpPath + "\\a");    // .\a\
            Directory.CreateDirectory(tmpPath + "\\b\\c"); // .\b\c\

            File.WriteAllText(tmpPath + "\\t", "1234");    // .\t
            File.WriteAllText(tmpPath + "\\a\\t", "123");  // .\a\t

            Directory.SetLastWriteTimeUtc(tmpPath, DateTime.FromFileTimeUtc(1));
            File.SetLastWriteTimeUtc(tmpPath + "\\t", DateTime.FromFileTimeUtc(2));

            var response = await Helper.DispatchCommandAsync<ListFilesCommand, ListFilesResponse>(new ListFilesCommand
            {
                Path = tmpPath,
                WorkDir = "",
                IncludeSubdirectories = false
            });

            Assert.Equal(2, response.Files.Length);
            Assert.Equal(new FileMetadata("./", 0, DateTime.FromFileTimeUtc(1)), response.Files[0]);
            Assert.Equal(new FileMetadata("t", 4, DateTime.FromFileTimeUtc(2)), response.Files[1]);

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
                WorkDir = Path.GetDirectoryName(tmpPath),
                IncludeSubdirectories = true
            });

            Assert.Empty(response.Files);
        }
    }
}
