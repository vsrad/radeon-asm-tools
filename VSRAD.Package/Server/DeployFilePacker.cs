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
        byte[] PackItems(IEnumerable<DeployItem> items);
    }

    [Export(typeof(IDeployFilePacker))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class DeployFilePacker : IDeployFilePacker
    {
        public byte[] PackItems(IEnumerable<DeployItem> items)
        {
            using (var memStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, false))
                {
                    foreach (var item in items)
                    {
                        archive.CreateEntryFromFile(item.ActualPath, item.ArchivePath.Replace('\\', '/'), CompressionLevel.Optimal);
                    }
                }
                return memStream.ToArray();
            }
        }


    }
}
