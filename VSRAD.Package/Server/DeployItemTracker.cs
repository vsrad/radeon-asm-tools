using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Server
{
    public interface IDeployItemTracker
    {
        IEnumerable<DeployItem> GetDeployItems(IEnumerable<string> paths, string projectPath);
    }

    [Export(typeof(IDeployItemTracker))]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class DeployItemTracker : IDeployItemTracker
    {
        private Dictionary<string, DeployItem> CurrentDeployItems = new Dictionary<string, DeployItem>();

        IEnumerable<DeployItem> IDeployItemTracker.GetDeployItems(IEnumerable<string> paths, string projectPath)
        {
            var actualDeployItems = new Dictionary<string, DeployItem>();

            var additionalFilesPaths = paths.GetFilePaths();
            var additionalDirectoryPaths = paths.GetDirectoriesPaths();

            foreach (var filePath in additionalFilesPaths)
                actualDeployItems.Add(filePath, GetFileItem(filePath, projectPath));

            foreach (var directoryPath in additionalDirectoryPaths)
                UpdateItemsInDirectoryRecursively(actualDeployItems, directoryPath, directoryPath);

            CurrentDeployItems.Clear();
            CurrentDeployItems = actualDeployItems;

            /*
             * Probably not best solution
             * 
             * Teoretically it's should look like this but it's work not correctly:
             * 
             * foreach (var item in CurrentDeployItems.Values)
             * {
             *  if (item.IsChanged())
             *      yield return item;
             * }
             * 
             */
            var deployItems = new List<DeployItem>();
            foreach (var item in CurrentDeployItems.Values.ToList())
            {
                if (item.IsChanged())
                    deployItems.Add(item);
            }
            return deployItems;
        }

        private void UpdateItemsInDirectoryRecursively(Dictionary<string, DeployItem> actualDeployItems, string dir, string rootPath)
        {
            foreach (var filePath in Directory.EnumerateFiles(dir))
                actualDeployItems.Add(filePath, GetFileItem(filePath, rootPath));

            foreach (var subdir in Directory.EnumerateDirectories(dir))
                UpdateItemsInDirectoryRecursively(actualDeployItems, subdir, rootPath);
        }

        private DeployItem GetFileItem(string filePath, string rootPath)
        {
            if (CurrentDeployItems.TryGetValue(filePath, out var deployItemFromDict))
                return deployItemFromDict;
            else
            {
                var newDeployItem = new DeployItem();
                newDeployItem.ActualPath = filePath;
                newDeployItem.MakeArchivePath(rootPath);
                return newDeployItem;
            }
        }
    }
}
