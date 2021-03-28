using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace VSRAD.DebugServer.SharedUtils
{
    public readonly struct PackedFile : IEquatable<PackedFile>
    {
        public byte[] Data { get; }
        public string RelativePath { get; }
        public DateTime LastWriteTimeUtc { get; }

        public PackedFile(byte[] data, string relativePath, DateTime lastWriteTimeUtc)
        {
            Data = data;
            RelativePath = relativePath;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }

        public static PackedFile[] PackFiles(string workDir, IEnumerable<string> paths, bool useCompression)
        {
            try
            {
                return paths.AsParallel().Select(path =>
                {
                    var fullPath = Path.Combine(workDir, path);
                    if (path.EndsWith("/", StringComparison.Ordinal))
                    {
                        return new PackedFile(Array.Empty<byte>(), path, Directory.GetLastWriteTimeUtc(fullPath));
                    }
                    else
                    {
                        var lastWriteTime = File.GetLastWriteTimeUtc(fullPath);
                        if (useCompression)
                        {
                            byte[] compressedData;

                            using (var dstStream = new MemoryStream())
                            {
                                using (var srcStream = File.OpenRead(fullPath))
                                using (var gzipStream = new GZipStream(dstStream, CompressionLevel.Optimal))
                                {
                                    srcStream.CopyTo(gzipStream);
                                }
                                compressedData = dstStream.ToArray();
                            }

                            return new PackedFile(compressedData, path, lastWriteTime);
                        }
                        else
                        {
                            return new PackedFile(File.ReadAllBytes(fullPath), path, lastWriteTime);
                        }
                    }
                }).ToArray();
            }
            catch (AggregateException e)
            {
                throw e.InnerExceptions[0];
            }
        }

        public static void UnpackFiles(string path, PackedFile[] files, bool decompress, bool preserveTimestamps)
        {
            var destination = Directory.CreateDirectory(path);

            try
            {
                files.AsParallel().ForAll(file =>
                {
                    var entryDestPath = Path.Combine(destination.FullName, file.RelativePath);

                    if (file.RelativePath.EndsWith("/", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(entryDestPath);
                        if (preserveTimestamps)
                            Directory.SetLastWriteTimeUtc(entryDestPath, file.LastWriteTimeUtc);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(entryDestPath));

                        if (decompress)
                        {
                            using (var dstStream = File.Create(entryDestPath))
                            using (var srcStream = new MemoryStream(file.Data))
                            using (var gzipStream = new GZipStream(srcStream, CompressionMode.Decompress))
                            {
                                gzipStream.CopyTo(dstStream);
                            }
                        }
                        else
                        {
                            File.WriteAllBytes(entryDestPath, file.Data);
                        }

                        if (preserveTimestamps)
                            File.SetLastWriteTimeUtc(entryDestPath, file.LastWriteTimeUtc);
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
