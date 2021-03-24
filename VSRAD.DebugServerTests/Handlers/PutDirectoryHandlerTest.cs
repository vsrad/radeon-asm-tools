﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class PutDirectoryHandlerTest
    {
        [Fact]
        public async Task SuccessTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var zipData = CreateZipArchive(new[]
            {
                (new byte[] { 0x4C }, "test", new DateTime(1998, 07, 06)),
                (new byte[] { 0x4C, 0x41, 0x49 }, "dir/test", new DateTime(1998, 07, 13)),
                (new byte[] { 0x4E }, "nested/dir/test", new DateTime(1998, 07, 20)),
                (Array.Empty<byte>(), "empty/dir/", new DateTime(1998, 07, 27)),
            });

            var response = await Helper.DispatchCommandAsync<PutDirectoryCommand, PutDirectoryResponse>(new PutDirectoryCommand
            {
                ZipData = zipData,
                Path = Path.GetFileName(tmpPath),
                WorkDir = Path.GetDirectoryName(tmpPath),
                PreserveTimestamps = true
            });

            Assert.Equal(PutDirectoryStatus.Successful, response.Status);

            Assert.True(File.Exists(Path.Combine(tmpPath, "test")));
            Assert.Equal(new byte[] { 0x4C }, File.ReadAllBytes(Path.Combine(tmpPath, "test")));
            Assert.Equal(new DateTime(1998, 07, 06), File.GetLastWriteTime(Path.Combine(tmpPath, "test")));

            Assert.True(File.Exists(Path.Combine(tmpPath, "dir", "test")));
            Assert.Equal(new byte[] { 0x4C, 0x41, 0x49 }, File.ReadAllBytes(Path.Combine(tmpPath, "dir", "test")));
            Assert.Equal(new DateTime(1998, 07, 13), File.GetLastWriteTime(Path.Combine(tmpPath, "dir", "test")));

            Assert.True(File.Exists(Path.Combine(tmpPath, "nested", "dir", "test")));
            Assert.Equal(new byte[] { 0x4E }, File.ReadAllBytes(Path.Combine(tmpPath, "nested", "dir", "test")));
            Assert.Equal(new DateTime(1998, 07, 20), File.GetLastWriteTime(Path.Combine(tmpPath, "nested", "dir", "test")));

            Assert.True(Directory.Exists(Path.Combine(tmpPath, "empty", "dir")));
            Assert.Equal(new DateTime(1998, 07, 27), Directory.GetLastWriteTime(Path.Combine(tmpPath, "empty", "dir")));

            Directory.Delete(tmpPath, recursive: true);
        }

        [Fact]
        public async Task TargetPathIsFileTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllText(tmpPath, "existing file");

            var response = await Helper.DispatchCommandAsync<PutDirectoryCommand, PutDirectoryResponse>(new PutDirectoryCommand
            {
                ZipData = Array.Empty<byte>(),
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

            var zipData = CreateZipArchive(new[] { (new byte[] { 0x48 }, "test", new DateTime(2002, 10, 09)) });

            var response = await Helper.DispatchCommandAsync<PutDirectoryCommand, PutDirectoryResponse>(new PutDirectoryCommand
            {
                ZipData = zipData,
                Path = tmpPath,
                WorkDir = ""
            });

            Assert.Equal(PutDirectoryStatus.PermissionDenied, response.Status);

            File.SetAttributes(Path.Combine(tmpPath, "test"), FileAttributes.Normal);
            Directory.Delete(tmpPath, recursive: true);
        }

        private static byte[] CreateZipArchive(IEnumerable<(byte[] Data, string Name, DateTime LastWriteTime)> items)
        {
            using var memStream = new MemoryStream();
            using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, false))
            {
                foreach (var (data, name, lastWriteTime) in items)
                {
                    var entry = archive.CreateEntry(name);
                    entry.LastWriteTime = lastWriteTime;
                    using var entryStream = entry.Open();
                    entryStream.Write(data);
                }
            }
            return memStream.ToArray();
        }
    }
}
