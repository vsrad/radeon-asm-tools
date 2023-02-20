using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Text;

namespace VSRAD.DebugServer.SharedUtils
{
    public class FileMetadata
    {
        /// <summary>
        /// Paths use / as the directory separator. Paths ending with the slash are interpreted as directories:
        /// this matches the behavior of zip archives used in <see cref="IPC.Commands.GetFilesCommand"/> and <see cref="IPC.Commands.PutDirectoryCommand"/>.
        /// </summary>
        public string RelativePath { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }

        public bool IsDirectory { get; set; }

        public FileMetadata() { }

        public FileMetadata(string relativePath, DateTime lastWriteTimeUtc, bool isDirectory)
        {
            RelativePath = relativePath;
            LastWriteTimeUtc = lastWriteTimeUtc;
            IsDirectory = isDirectory;
        }

        public String ToString()
        {
            var props = this.GetType().GetProperties();
            var sb = new StringBuilder();
            foreach (var p in props)
            {
                sb.AppendLine(p.Name + ": " + p.GetValue(this, null));
            }
            return sb.ToString();
        }

        public static bool isOutdated(FileMetadata metadata, String BaseDir)
        {
            var fullPath = Path.Combine(BaseDir, metadata.RelativePath);
            Console.WriteLine($"Checking file {fullPath} for existence");
            if (!metadata.IsDirectory && File.Exists(fullPath))
            {
                var localInfo = new FileInfo(fullPath);
                return metadata.LastWriteTimeUtc == localInfo.LastWriteTimeUtc;
            }
            if (metadata.IsDirectory && Directory.Exists(fullPath))
            {
                var localInfo = new DirectoryInfo(fullPath);
                return metadata.LastWriteTimeUtc == localInfo.LastWriteTimeUtc;
            }
            return false;
        }
    }
}
