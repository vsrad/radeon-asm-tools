using System;
using System.IO;

namespace VSRAD.Package.Server
{

    public class DeployItem
    {
        public string ActualPath { get; set; }
        public string ArchivePath { get; set; }
        public DateTime LastWriteTime { get; set; }

        public bool IsChanged()
        {
            var lastWriteTime = File.GetLastWriteTime(this.ActualPath);
            if (lastWriteTime != this.LastWriteTime)
            {
                this.LastWriteTime = lastWriteTime;
                return true;
            }
            return false;
        }

        /* Files within an archive must have a relative path */
        public void MakeArchivePath(string root)
        {
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                root += Path.DirectorySeparatorChar;
            }
            if (ActualPath.StartsWith(root))
                ArchivePath = ActualPath.Replace(root, "");
            else
                ArchivePath = Path.GetFileName(ActualPath);
        }

        public override int GetHashCode()
        {
            return this.ActualPath.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj != null 
                && string.Equals(this.ActualPath, (obj as DeployItem).ActualPath);
        }
    }
}
