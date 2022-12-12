using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Text;

namespace VSRAD.DebugServer.SharedUtils
{

    public class FileMetadata// : IEquatable<FileMetadata>
    {
        /// <summary>
        /// Paths use / as the directory separator. Paths ending with the slash are interpreted as directories:
        /// this matches the behavior of zip archives used in <see cref="IPC.Commands.GetFilesCommand"/> and <see cref="IPC.Commands.PutDirectoryCommand"/>.
        /// </summary>
        public string relativePath_ { get; set; }

        public DateTime lastWriteTimeUtc_ { get; set; }

        public bool isDirectory_ { get; set; }

        public FileMetadata() { }

        public FileMetadata(string relativePath, DateTime lastWriteTimeUtc, bool isDirectory)
        {
            relativePath_ = relativePath;
            lastWriteTimeUtc_ = lastWriteTimeUtc;
            isDirectory_ = isDirectory;
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
            var fullPath = Path.Combine(BaseDir, metadata.relativePath_);
            Console.WriteLine($"Checking file {fullPath} for existence");
            if (!metadata.isDirectory_ && File.Exists(fullPath))
            {
                var localInfo = new FileInfo(fullPath);
                return metadata.lastWriteTimeUtc_ == localInfo.LastWriteTimeUtc;
            }
            if (metadata.isDirectory_ && Directory.Exists(fullPath))
            {
                var localInfo = new DirectoryInfo(fullPath);
                return metadata.lastWriteTimeUtc_ == localInfo.LastWriteTimeUtc;
            }
            return false;
        }
    }
}
