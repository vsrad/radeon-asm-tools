using System.IO;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class FetchMetadataTest
    {
        [Fact]
        public async void FetchMetadataBinaryTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tmpFile, new byte[16]);
            var lastWriteTime = File.GetLastWriteTime(tmpFile).ToUniversalTime();

            var response = await Helper.DispatchCommandAsync<FetchMetadata, MetadataFetched>(
                new FetchMetadata
                {
                    FilePath = new[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(lastWriteTime, response.Timestamp);
            Assert.Equal(16, response.ByteCount);
        }

        [Fact]
        public async void FetchMetadataFileNotFoundTest()
        {
            var response = await Helper.DispatchCommandAsync<FetchMetadata, MetadataFetched>(
                new FetchMetadata
                {
                    FilePath = new[] { @"I:\Never", "Existed" }
                });
            Assert.Equal(FetchStatus.FileNotFound, response.Status);
        }

        [Fact]
        public async void FetchMetadataTextTestAsync()
        {
            var tmpFile = Path.GetTempFileName();

            await File.WriteAllLinesAsync(tmpFile, new[] { "Metadata", "0x00000000", "0x00000001", "0x00000002", "   ", "" });
            var response = await Helper.DispatchCommandAsync<FetchMetadata, MetadataFetched>(
                new FetchMetadata
                {
                    FilePath = new[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    BinaryOutput = false
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(16, response.ByteCount);

            await File.WriteAllLinesAsync(tmpFile, new[] { "0x0", "0x1" });
            response = await Helper.DispatchCommandAsync<FetchMetadata, MetadataFetched>(
                new FetchMetadata
                {
                    FilePath = new[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    BinaryOutput = false
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(8, response.ByteCount);
        }
    }
}
