using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;
using VSRAD.DebugServer.SharedUtils;

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

            var files = new List<FileMetadata>();
            files.Add(new FileMetadata(tmpPath, File.GetLastWriteTimeUtc(tmpPath), false));

            var response = await Helper.DispatchCommandAsync<CheckOutdatedFiles, CheckOutdatedFilesResponse>(new CheckOutdatedFiles
            {
                DstPath = Path.GetDirectoryName(tmpPath),
                Files = files
            });

            Assert.Single(response.Files);
            Assert.Equal(files, response.Files);

            File.Delete(tmpPath);
        }

        [Fact]
        public async Task DirectoryHierarchyTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var files = new List<FileMetadata>();

            Directory.CreateDirectory(tmpPath);            // .
            Directory.SetLastWriteTimeUtc(tmpPath, DateTime.FromFileTimeUtc(1));
            files.Add(new FileMetadata(tmpPath, Directory.GetLastWriteTimeUtc(tmpPath), true));

            Directory.CreateDirectory(tmpPath + "\\a");    // .\a\
            Directory.SetLastWriteTimeUtc(tmpPath + "\\a", DateTime.FromFileTimeUtc(2));
            files.Add(new FileMetadata(tmpPath + "\\a", Directory.GetLastWriteTimeUtc(tmpPath + "\\a"), true));

            Directory.CreateDirectory(tmpPath + "\\b\\c"); // .\b\c\
            Directory.SetLastWriteTimeUtc(tmpPath + "\\b\\c", DateTime.FromFileTimeUtc(3));
            files.Add(new FileMetadata(tmpPath + "\\b\\c", Directory.GetLastWriteTimeUtc(tmpPath + "\\b\\c"), true));

            Directory.SetLastWriteTimeUtc(tmpPath + "\\b", DateTime.FromFileTimeUtc(4));
            files.Add(new FileMetadata(tmpPath + "\\b", Directory.GetLastWriteTimeUtc(tmpPath + "\\b"), true));

            File.WriteAllText(tmpPath + "\\t", "1234");    // .\t
            File.SetLastWriteTimeUtc(tmpPath + "\\t", DateTime.FromFileTimeUtc(5));
            files.Add(new FileMetadata(tmpPath + "\\t", File.GetLastWriteTimeUtc(tmpPath + "\\t"), true));

            File.WriteAllText(tmpPath + "\\a\\t", "123");  // .\a\t
            File.SetLastWriteTimeUtc(tmpPath + "\\a\\t", DateTime.FromFileTimeUtc(6));
            files.Add(new FileMetadata(tmpPath + "\\a\\t", File.GetLastWriteTimeUtc(tmpPath + "\\a\\t"), true));

            File.WriteAllText(tmpPath + "\\b\\c\\t", "1"); // .\b\c\t
            File.SetLastWriteTimeUtc(tmpPath + "\\a\\t", DateTime.FromFileTimeUtc(7));
            files.Add(new FileMetadata(tmpPath + "\\b\\c\\t", File.GetLastWriteTimeUtc(tmpPath + "\\b\\c\\t"), true));
         

            var response = await Helper.DispatchCommandAsync<CheckOutdatedFiles, CheckOutdatedFilesResponse>(new CheckOutdatedFiles
            {
                DstPath = Path.GetDirectoryName(tmpPath),
                Files = files
            });

            Assert.False(response.Files.Any());
        
            Directory.Delete(tmpPath, recursive: true);
        }

        [Fact]
        public async Task FileNotFoundTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Assert.False(File.Exists(tmpPath));
            var files = new List<FileMetadata>();
            files.Add(new FileMetadata(tmpPath, DateTime.FromFileTimeUtc(1), false));

            var response = await Helper.DispatchCommandAsync<CheckOutdatedFiles, CheckOutdatedFilesResponse>(new CheckOutdatedFiles
            {
                DstPath = Path.GetDirectoryName(tmpPath),
                Files = files
            });

            Assert.True(response.Files.Any());
        }
    }
}
