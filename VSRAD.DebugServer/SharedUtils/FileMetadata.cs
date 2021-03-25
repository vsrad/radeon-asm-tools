using System;
using System.Collections.Generic;
using System.IO;

namespace VSRAD.DebugServer.SharedUtils
{
    public readonly struct FileMetadata : IEquatable<FileMetadata>
    {
        /// <summary>
        /// Paths use / as the directory separator. Paths ending with the slash are interpreted as directories:
        /// this matches the behavior of zip archives used in <see cref="IPC.Commands.GetFilesCommand"/> and <see cref="IPC.Commands.PutDirectoryCommand"/>.
        /// </summary>
        public string RelativePath { get; }
        public bool IsDirectory => RelativePath.EndsWith("/");
        public long Size { get; }
        public DateTime LastWriteTimeUtc { get; }

        public FileMetadata(string relativePath, long size, DateTime lastWriteTimeUtc)
        {
            RelativePath = relativePath;
            Size = size;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }

        public static List<FileMetadata> GetMetadataForPath(string path, bool recursive)
        {
            var files = new List<FileMetadata>();

            if (Directory.Exists(path))
            {
                files.Add(new FileMetadata("./", 0, File.GetLastWriteTimeUtc(path)));

                var root = new DirectoryInfo(path);
                var searchOpts = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var entry in root.EnumerateFileSystemInfos("*", searchOpts))
                {
                    var relPath = entry.FullName.Substring(root.FullName.Length + 1).Replace('\\', '/');
                    if (entry is FileInfo file)
                        files.Add(new FileMetadata(relPath, file.Length, file.LastWriteTimeUtc));
                    else if (recursive)
                        files.Add(new FileMetadata(relPath + '/', 0, entry.LastWriteTimeUtc));
                }
            }
            else if (File.Exists(path))
            {
                var file = new FileInfo(path);
                files.Add(new FileMetadata(".", file.Length, file.LastWriteTimeUtc));
            }

            return files;
        }

        public bool Equals(FileMetadata other) =>
            RelativePath == other.RelativePath &&
            IsDirectory == other.IsDirectory &&
            Size == other.Size &&
            LastWriteTimeUtc == other.LastWriteTimeUtc;

        public override bool Equals(object obj) => obj is FileMetadata metadata && Equals(metadata);
        public override int GetHashCode() => (RelativePath, IsDirectory, Size, LastWriteTimeUtc).GetHashCode();
        public static bool operator ==(FileMetadata left, FileMetadata right) => left.Equals(right);
        public static bool operator !=(FileMetadata left, FileMetadata right) => !(left == right);
    }
}
