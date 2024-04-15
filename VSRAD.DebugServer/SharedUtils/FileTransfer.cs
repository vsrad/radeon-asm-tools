using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.SharedUtils
{
    public static class FileTransfer
    {
        public static GetFilesResponse GetFiles(GetFilesCommand command)
        {
            try
            {
                var files = PackedFile.PackFiles(command.RootPath, command.Paths);
                return new GetFilesResponse { Status = GetFilesStatus.Successful, Files = files };
            }
            catch (FileNotFoundException e)
            {
                return new GetFilesResponse { Status = GetFilesStatus.FileOrDirectoryNotFound, ErrorMessage = e.Message };
            }
            catch (DirectoryNotFoundException e)
            {
                return new GetFilesResponse { Status = GetFilesStatus.FileOrDirectoryNotFound, ErrorMessage = e.Message };
            }
            catch (UnauthorizedAccessException e)
            {
                return new GetFilesResponse { Status = GetFilesStatus.PermissionDenied, ErrorMessage = e.Message };
            }
            catch (IOException e)
            {
                return new GetFilesResponse { Status = GetFilesStatus.PermissionDenied, ErrorMessage = e.Message };
            }
        }

        public static async Task<PutFilesResponse> PutFilesAsync(PutFilesCommand command)
        {
            bool retryOnce = true;
            while (true)
            {
                try
                {
                    PackedFile.UnpackFiles(command.RootPath, command.Files, command.PreserveTimestamps);
                    return new PutFilesResponse { Status = PutFilesStatus.Successful };
                }
                catch (UnauthorizedAccessException e)
                {
                    return new PutFilesResponse { Status = PutFilesStatus.PermissionDenied, ErrorMessage = e.Message };
                }
                catch (IOException e)
                {
                    // Retrying the operation helps with "file is being used by another process" errors when the process that accessed the file has just exited
                    if (retryOnce)
                    {
                        retryOnce = false;
                        await Task.Delay(100);
                    }
                    else
                    {
                        return new PutFilesResponse { Status = PutFilesStatus.OtherIOError, ErrorMessage = e.Message };
                    }
                }
            }
        }
    }
}
