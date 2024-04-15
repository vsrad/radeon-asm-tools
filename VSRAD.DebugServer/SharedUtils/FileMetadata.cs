using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.IO;

namespace VSRAD.DebugServer.SharedUtils
{
    public readonly struct FileMetadata : IEquatable<FileMetadata>
    {
        /// <summary>
        /// The directory separator is / regardless of the platform. Paths ending with / are treated as directories.
        /// If the relative path is an empty string, the root path is a file.
        /// </summary>
        public string RelativePath { get; }
        public long Size { get; }
        public DateTime LastWriteTimeUtc { get; }

        public bool IsDirectory => RelativePath.EndsWith("/", StringComparison.Ordinal);

        public FileMetadata(string relativePath, long size, DateTime lastWriteTimeUtc)
        {
            RelativePath = relativePath;
            Size = size;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }

        public static List<FileMetadata> CollectFileMetadata(string rootPath, string[] globs)
        {
            var files = new List<FileMetadata>();

            if (Directory.Exists(rootPath))
            {
                var root = new DirectoryInfo(rootPath);
                files.Add(new FileMetadata("./", 0, root.LastWriteTimeUtc));

                var rootPathLength = root.FullName.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? root.FullName.Length : root.FullName.Length + 1;

                var globMatcher = new Matcher();
                globMatcher.AddIncludePatterns(globs);
                foreach (var file in root.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (globMatcher.Match(rootPath, file.FullName).HasMatches)
                    {
                        var relPath = file.FullName.Substring(rootPathLength).Replace('\\', '/');
                        files.Add(new FileMetadata(relPath, file.Length, file.LastWriteTimeUtc));
                    }
                }
                foreach (var dir in root.EnumerateDirectories("*", SearchOption.AllDirectories))
                {
                    var relPath = dir.FullName.Substring(rootPathLength).Replace('\\', '/');
                    if (files.Exists(f => !f.IsDirectory && f.RelativePath.StartsWith(relPath, StringComparison.Ordinal)))
                        files.Add(new FileMetadata(relPath + '/', 0, dir.LastWriteTimeUtc));
                }
                files.Sort((a, b) => b.IsDirectory.CompareTo(a.IsDirectory));
            }
            else if (File.Exists(rootPath))
            {
                var file = new FileInfo(rootPath);
                files.Add(new FileMetadata("", file.Length, file.LastWriteTimeUtc));
            }

            return files;
        }

        public bool Equals(FileMetadata other) =>
            RelativePath == other.RelativePath &&
            Size == other.Size &&
            LastWriteTimeUtc == other.LastWriteTimeUtc;

        public override bool Equals(object obj) => obj is FileMetadata metadata && Equals(metadata);
        public override int GetHashCode() => (RelativePath, Size, LastWriteTimeUtc).GetHashCode();
        public static bool operator ==(FileMetadata left, FileMetadata right) => left.Equals(right);
        public static bool operator !=(FileMetadata left, FileMetadata right) => !(left == right);
    }
}
