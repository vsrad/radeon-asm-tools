using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    public enum DocumentSaveType
    {
        ActiveDocument = 1,
        OpenDocuments = 2,
        ProjectDocuments = 3,
        SolutionDocuments = 4,
        None = 5,
    }

    public interface IProjectSourceManager
    {
        string ProjectRoot { get; }
        Task SaveDocumentsAsync(DocumentSaveType type);
        Task<IEnumerable<(string absolutePath, string relativePath)>> ListProjectFilesAsync();
    }

    [Export(typeof(IProjectSourceManager))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class ProjectSourceManager : IProjectSourceManager
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly UnconfiguredProject _unconfiguredProject;

        public string ProjectRoot { get; }

        [ImportingConstructor]
        public ProjectSourceManager(SVsServiceProvider serviceProvider, UnconfiguredProject unconfiguredProject)
        {
            _serviceProvider = serviceProvider;
            _unconfiguredProject = unconfiguredProject;
            ProjectRoot = Path.GetDirectoryName(unconfiguredProject.FullPath);
        }

        public async Task SaveDocumentsAsync(DocumentSaveType type)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);
            switch (type)
            {
                case DocumentSaveType.ActiveDocument:
                    if (dte.ActiveDocument?.Saved == false)
                        dte.ActiveDocument.Save();
                    break;
                case DocumentSaveType.OpenDocuments:
                    dte.Documents.SaveAll();
                    break;
                case DocumentSaveType.ProjectDocuments:
                    if (dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
                        if (activeSolutionProjects.GetValue(0) is EnvDTE.Project activeProject)
                            foreach (ProjectItem item in activeProject.ProjectItems)
                                SaveDocumentsRecursively(item);
                    break;
                case DocumentSaveType.SolutionDocuments:
                    foreach (EnvDTE.Project project in dte.Solution.Projects)
                        foreach (ProjectItem item in project.ProjectItems)
                            SaveDocumentsRecursively(item);
                    break;
            }
        }

        public async Task<IEnumerable<(string absolutePath, string relativePath)>> ListProjectFilesAsync()
        {
            var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
            var itemsProvider = configuredProject.GetService<IProjectItemProvider>("SourceItems");
            var projectItems = await itemsProvider.GetItemsAsync();

            var files = new List<(string absolutePath, string relativePath)>();
            foreach (var item in projectItems)
            {
                string name;
                if (item.EvaluatedIncludeAsFullPath.StartsWith(ProjectRoot, StringComparison.Ordinal))
                    name = item.EvaluatedIncludeAsRelativePath;
                else
                    name = await item.Metadata.GetEvaluatedPropertyValueAsync("Link");
                if (!string.IsNullOrEmpty(name))
                    files.Add((item.EvaluatedIncludeAsFullPath, name));
            }

            return files;
        }

        private void SaveDocumentsRecursively(ProjectItem projectItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (projectItem.Document?.Saved == false)
                projectItem.Document.Save();

            if (projectItem.ProjectItems != null)
                foreach (ProjectItem subItem in projectItem.ProjectItems)
                    SaveDocumentsRecursively(subItem);
        }
    }
}
