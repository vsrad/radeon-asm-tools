using System;
using System.Collections.Generic;
using System.IO;

namespace VSRAD.DebugServer.SharedUtils
{
    public readonly struct FileMetadata : IEquatable<FileMetadata>
    {
        public string RelativePath { get; }
        public bool IsDirectory { get; }
        public long Size { get; }
        public DateTime LastWriteTimeUtc { get; }

        public FileMetadata(string relativePath, bool isDirectory, long size, DateTime lastWriteTimeUtc)
        {
            RelativePath = relativePath;
            IsDirectory = isDirectory;
            Size = size;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }

        public static List<FileMetadata> GetMetadataForTree(string path)
        {
            var files = new List<FileMetadata>();

            if (Directory.Exists(path))
            {
                files.Add(new FileMetadata(".", isDirectory: true, 0, File.GetLastWriteTimeUtc(path)));

                var root = new DirectoryInfo(path);
                foreach (var entry in root.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    var relPath = entry.FullName.Substring(root.FullName.Length + 1);
                    if (entry is FileInfo file)
                        files.Add(new FileMetadata(relPath, isDirectory: false, file.Length, file.LastWriteTimeUtc));
                    else
                        files.Add(new FileMetadata(relPath, isDirectory: true, 0, entry.LastWriteTimeUtc));
                }
            }
            else if (File.Exists(path))
            {
                var file = new FileInfo(path);
                files.Add(new FileMetadata(".", isDirectory: false, file.Length, file.LastWriteTimeUtc));
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
