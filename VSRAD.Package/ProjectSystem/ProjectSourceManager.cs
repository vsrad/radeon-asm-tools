using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
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
        Task SaveDocumentsAsync(DocumentSaveType type);
        Task<IEnumerable<string>> ListProjectFilesAsync();
    }

    [Export(typeof(IProjectSourceManager))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class ProjectSourceManager : IProjectSourceManager
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly UnconfiguredProject _unconfiguredProject;

        [ImportingConstructor]
        public ProjectSourceManager(SVsServiceProvider serviceProvider, UnconfiguredProject unconfiguredProject)
        {
            _serviceProvider = serviceProvider;
            _unconfiguredProject = unconfiguredProject;
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

        public async Task<IEnumerable<string>> ListProjectFilesAsync()
        {
            var configuredProject = await _unconfiguredProject.GetSuggestedConfiguredProjectAsync();
            var itemsProvider = configuredProject.GetService<IProjectItemProvider>("SourceItems");
            var sourceItems = await itemsProvider.GetItemsAsync();
            var files = sourceItems.Select((i) => i.EvaluatedIncludeAsRelativePath).ToArray();
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
