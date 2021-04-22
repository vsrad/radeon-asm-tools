using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace VSRAD.DebugServer.SharedUtils
{
    public readonly struct ProcessTreeItem : IEquatable<ProcessTreeItem>
    {
        public int Id { get; }
        public string Name { get; }
        public int ChildLevel { get; }

        public ProcessTreeItem(int id, string name, int childLevel)
        {
            Id = id;
            Name = name;
            ChildLevel = childLevel;
        }

        public bool Equals(ProcessTreeItem other) => Id == other.Id && Name == other.Name && ChildLevel == other.ChildLevel;
        public override bool Equals(object obj) => obj is ProcessTreeItem data && Equals(data);
        public override int GetHashCode() => Id;
        public static bool operator ==(ProcessTreeItem left, ProcessTreeItem right) => left.Equals(right);
        public static bool operator !=(ProcessTreeItem left, ProcessTreeItem right) => !(left == right);
    }

    public static class ProcessUtils
    {
        public static bool IsParentOf(this Process parentProcess, Process otherProcess)
        {
#if NETCORE
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var linuxPpidLine = File.ReadLines($"/proc/{otherProcess.Id}/status").Skip(6).First();
                var otherProcessParentId = int.Parse(linuxPpidLine.Replace("PPid:\t", ""));
                return parentProcess.Id == otherProcessParentId;
            }
#endif

            return parentProcess.StartTime < otherProcess.StartTime && parentProcess.Id == otherProcess.GetParentProcessId();
        }

        public static List<ProcessTreeItem> GetProcessTree(this Process process)
        {
            void AppendChildProcesses(Process parent, List<ProcessTreeItem> tree, int nesting)
            {
                if (!parent.HasExited)
                {
                    tree.Add(new ProcessTreeItem(parent.Id, parent.ProcessName, nesting));
                    foreach (var p in Process.GetProcesses())
                    {
                        try
                        {
                            if (parent.IsParentOf(p))
                                AppendChildProcesses(p, tree, nesting + 1);
                        }
                        catch { /* cannot access process info */ }
                    }
                }
            }

            var processTree = new List<ProcessTreeItem>();
            AppendChildProcesses(process, processTree, 0);

            return processTree;
        }

        public static List<ProcessTreeItem> TerminateProcessTree(IEnumerable<ProcessTreeItem> tree)
        {
            var terminatedProcesses = new List<ProcessTreeItem>();
            foreach (var process in tree)
            {
                try
                {
                    Process.GetProcessById(process.Id).Kill();
                    terminatedProcesses.Add(process);
                }
                catch (Exception) { }
            }
            return terminatedProcesses;
        }

        public static void PrintProcessTree(StringBuilder outString, IEnumerable<ProcessTreeItem> tree)
        {
            foreach (var item in tree)
            {
                outString.Append('-', item.ChildLevel * 2);
                outString.AppendFormat(" [{0}] {1}", item.Id, item.Name);
                outString.AppendLine();
            }
        }
    }
}
