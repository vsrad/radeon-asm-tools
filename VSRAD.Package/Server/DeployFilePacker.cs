using Microsoft.VisualStudio.ProjectSystem;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;

namespace VSRAD.Package.Server
{
    public enum PackMode { File, DirectoryRecursively }

    public interface IDeployFilePacker
    {
        byte[] PackFiles(IEnumerable<string> filePaths, string rootPath);

        byte[] PackDirectory(string rootPath);

        byte[] PackItems(IEnumerable<DeployItem> items);
    }

    [Export(typeof(IDeployFilePacker))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class DeployFilePacker : IDeployFilePacker
    {
        byte[] IDeployFilePacker.PackFiles(IEnumerable<string> filePaths, string rootPath)
        {
            using (var memStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, false))
                {
                    foreach (var file in filePaths)
                    {
                        archive.CreateEntryFromFile(file, MakeArchivePath(file, rootPath), CompressionLevel.Optimal);
                    }
                }
                return memStream.ToArray();
            }
        }

        byte[] IDeployFilePacker.PackDirectory(string rootPath)
        {
            using (var memStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, false))
                {
                    PackDirRecursively(archive, rootPath, rootPath);
                }
                return memStream.ToArray();
            }
        }

        private static void PackDirRecursively(ZipArchive archive, string dir, string rootPath)
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (file.Contains(".radproj"))
                {
                    continue;
                }
                archive.CreateEntryFromFile(file, MakeArchivePath(file, rootPath), CompressionLevel.Optimal);
            }
            foreach (var subdir in Directory.EnumerateDirectories(dir))
            {
                PackDirRecursively(archive, subdir, rootPath);
            }
        }

        /* Files within an archive must have a relative path */
        private static string MakeArchivePath(string file, string root)
        {
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                 root += Path.DirectorySeparatorChar;
            }
            if (file.StartsWith(root))
                return file.Replace(root, "");
            else
                return Path.GetFileName(file);
        }

        public byte[] PackItems(IEnumerable<DeployItem> items)
        {
            using (var memStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, false))
                {
                    foreach (var item in items)
                    {
                        archive.CreateEntryFromFile(item.ActualPath, item.ArchivePath, CompressionLevel.Optimal);
                    }
                }
                return memStream.ToArray();
            }
        }
    }
}
