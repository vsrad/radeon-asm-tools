using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VSRAD.DebugServer.SharedUtils
{
    public readonly struct PackedFile : IEquatable<PackedFile>
    {
        /// <summary>
        /// See <see cref="FileMetadata.RelativePath"/>.
        /// </summary>
        public string RelativePath { get; }
        public DateTime LastWriteTimeUtc { get; }
        /// <summary>
        /// Empty if the path refers to a directory.
        /// </summary>
        public byte[] Data { get; }

        public bool IsDirectory => RelativePath.EndsWith("/", StringComparison.Ordinal);

        public PackedFile(string relativePath, DateTime lastWriteTimeUtc, byte[] data)
        {
            RelativePath = relativePath;
            LastWriteTimeUtc = lastWriteTimeUtc;
            Data = data;
        }

        public static PackedFile[] PackFiles(string workDir, IEnumerable<string> paths)
        {
            try
            {
                return paths.AsParallel().Select(path =>
                {
                    var fullPath = Path.Combine(workDir, path);
                    if (path.EndsWith("/", StringComparison.Ordinal))
                        return new PackedFile(path, Directory.GetLastWriteTimeUtc(fullPath), Array.Empty<byte>());
                    else
                        return new PackedFile(path, File.GetLastWriteTimeUtc(fullPath), File.ReadAllBytes(fullPath));
                }).ToArray();
            }
            catch (AggregateException e)
            {
                throw e.InnerExceptions[0];
            }
        }

        public static void UnpackFiles(string rootPath, IEnumerable<PackedFile> files, bool preserveTimestamps)
        {
            try
            {
                files.AsParallel().ForAll(file =>
                {
                    var fullPath = Path.Combine(rootPath, file.RelativePath);
                    if (file.IsDirectory)
                    {
                        Directory.CreateDirectory(fullPath);
                        if (preserveTimestamps)
                            Directory.SetLastWriteTimeUtc(fullPath, file.LastWriteTimeUtc);
                    }
                    else
                    {
                        var parentDir = Path.GetDirectoryName(fullPath);
                        if (parentDir.Length != 0)
                            Directory.CreateDirectory(parentDir);
                        File.WriteAllBytes(fullPath, file.Data);
                        if (preserveTimestamps)
                            File.SetLastWriteTimeUtc(fullPath, file.LastWriteTimeUtc);
                    }
                });
            }
            catch (AggregateException e)
            {
                throw e.InnerExceptions[0];
            }
        }

        public bool Equals(PackedFile other) =>
            RelativePath == other.RelativePath &&
            LastWriteTimeUtc == other.LastWriteTimeUtc &&
            Data == other.Data;

        public override bool Equals(object obj) => obj is PackedFile data && Equals(data);
        public override int GetHashCode() => (RelativePath, LastWriteTimeUtc).GetHashCode();
        public static bool operator ==(PackedFile left, PackedFile right) => left.Equals(right);
        public static bool operator !=(PackedFile left, PackedFile right) => !(left == right);
    }
}
