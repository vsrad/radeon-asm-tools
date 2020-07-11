using System.IO;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class PutFileHandlerTest
    {
        [Fact]
        public async Task SuccessTestAsync()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var response = await Helper.DispatchCommandAsync<PutFileCommand, PutFileResponse>(new PutFileCommand
            {
                Data = Encoding.UTF8.GetBytes("putfilehandlertest"),
                Path = Path.GetFileName(tmpPath),
                WorkDir = Path.GetDirectoryName(tmpPath)
            });

            Assert.Equal(PutFileStatus.Successful, response.Status);
            Assert.Equal("putfilehandlertest", File.ReadAllText(tmpPath));
            File.Delete(tmpPath);
        }

        [Fact]
        public async Task PermissionDeniedTestAsync()
        {
            var tmpPath = Path.GetTempFileName();
            File.SetAttributes(tmpPath, FileAttributes.ReadOnly);

            var response = await Helper.DispatchCommandAsync<PutFileCommand, PutFileResponse>(new PutFileCommand
            {
                Data = Encoding.UTF8.GetBytes("putfilehandlertest"),
                Path = Path.GetFileName(tmpPath),
                WorkDir = Path.GetDirectoryName(tmpPath)
            });

            Assert.Equal(PutFileStatus.PermissionDenied, response.Status);
            File.SetAttributes(tmpPath, FileAttributes.Normal);
            File.Delete(tmpPath);
        }

        [Fact]
        public async Task DirectoryNotFoundTestAsync()
        {
            var relFile = Path.GetRandomFileName();
            var relDir = Path.GetRandomFileName();
            var absDir = Path.Combine(Path.GetTempPath(), relDir);

            Assert.False(Directory.Exists(absDir));

            var response = await Helper.DispatchCommandAsync<PutFileCommand, PutFileResponse>(new PutFileCommand
            {
                Data = Encoding.UTF8.GetBytes("putfilehandlertest"),
                Path = Path.Combine(relDir, relFile),
                WorkDir = Path.GetTempPath()
            });

            Assert.Equal(PutFileStatus.Successful, response.Status);
            Assert.True(Directory.Exists(absDir));
            Assert.Equal("putfilehandlertest", File.ReadAllText(Path.Combine(absDir, relFile)));
            Directory.Delete(absDir, recursive: true);
        }

        [Fact]
        public async Task IOErrorTestAsync()
        {
            var tmpPath = Path.GetTempFileName();

            using (var fs = new FileStream(tmpPath, FileMode.Open))
            {
                var response = await Helper.DispatchCommandAsync<PutFileCommand, PutFileResponse>(new PutFileCommand
                {
                    Data = Encoding.UTF8.GetBytes("putfilehandlertest"),
                    Path = tmpPath,
                    WorkDir = Path.GetTempPath()
                });
                Assert.Equal(PutFileStatus.OtherIOError, response.Status);
            }

            File.Delete(tmpPath);
        }
    }
}
