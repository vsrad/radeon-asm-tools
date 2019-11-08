using System;
using System.IO;
using System.Linq;
using System.Text;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class FetchResultRangeHandlerTest
    {
        [Fact]
        public async void FetchResultRangeBinaryTest()
        {
            var tmpFile = Path.GetTempFileName();
            var data = Encoding.UTF8.GetBytes("Real Data");
            File.WriteAllBytes(tmpFile, data);
            var timestamp = File.GetLastWriteTime(tmpFile).ToUniversalTime();

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 0,
                    ByteCount = data.Length + 1,
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(timestamp, response.Timestamp);
            Assert.Equal(data, response.Data);

            int offset = 2;
            int count = 3;

            response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = offset,
                    ByteCount = count,
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(data.Skip(offset).Take(count), response.Data);
        }

        [Fact]
        public async void FetchResultRangeTextTest()
        {
            var tmpFile = Path.GetTempFileName();
            var data = new string[] {
                "Metadata",
                "0x00000000",
                "0x00000001",
                "0x00000002",
                "0x00000003",
                "   ",
                "0x00000004",
                ""
            };
            await File.WriteAllLinesAsync(tmpFile, data);
            var timestamp = File.GetLastWriteTime(tmpFile).ToUniversalTime();
            var byteData = new byte[8] { 1, 0, 0, 0, 2, 0, 0, 0 };

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 6,
                    ByteCount = 8,
                    OutputOffset = 1,
                    BinaryOutput = false
                }); ;
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(timestamp, response.Timestamp);
            Assert.Equal(byteData, response.Data);

            response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 0,
                    ByteCount = 666,
                    BinaryOutput = false
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(20, response.Data.Length);
        }

        [Fact]
        public async void FetchAllFileTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            var data = Encoding.UTF8.GetBytes("Real Data Here");
            File.WriteAllBytes(tmpFile, data);

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 0,
                    ByteCount = 0,
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(data, response.Data);
        }

        [Fact]
        public async void FetchEmptyTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            using (var stream = File.Create(tmpFile))
            {
                stream.Close();
                stream.Dispose();
                File.SetLastWriteTime(tmpFile, DateTime.Now);
            };

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = int.Parse("DEAD", System.Globalization.NumberStyles.HexNumber),
                    ByteCount = 666,
                    BinaryOutput = true
                });
            File.Delete(tmpFile);
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Empty(response.Data);
        }

        [Fact]
        public async void FetchFileNotFoundTestAsync()
        {
            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { @"Do:\You", "Exist?" },
                    ByteOffset = int.Parse("DEAD", System.Globalization.NumberStyles.HexNumber),
                    ByteCount = 666,
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.FileNotFound, response.Status);
            Assert.Equal(Array.Empty<byte>(), response.Data);
        }

        [Fact]
        public async void OutputOffsetTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            var byteData = new byte[8] { 90, 85, 80, 70, 40, 10, 0, 0 };

            await File.WriteAllBytesAsync(tmpFile, byteData);

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 1,
                    ByteCount = 666,
                    BinaryOutput = true,
                    OutputOffset = 4
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(new byte[3] { 10, 0, 0 }, response.Data);

            var stringData = new string[]
            {
                "<...-Restricted accesы: do not proceed without special permission-...>",
                "<...-Property of NERV corporation. All rights reserved-...>",
                "<...-EVA00 logfile-...>",
                "<...-System state snapshot 11/01/1996 00:00:15-...>",
                "0x00000016",
                "0x00000022",
                "0x00000064",
                "0x00000044",
                "0x00000000",
                "0x00000055",
                "0x00000077",
                "0x00000014",
            };

            await File.WriteAllLinesAsync(tmpFile, stringData);

            response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
               new FetchResultRange
               {
                   FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                   ByteOffset = 4,
                   ByteCount = 8,
                   BinaryOutput = false,
                   OutputOffset = 4
               });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(new byte[8] { 34, 0, 0, 0, 100, 0, 0, 0 }, response.Data);
        }
    }
}
