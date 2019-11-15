using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
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
        Task SaveActiveSourceAsync();
        Task SaveProjectSourceAsync();
        Task SaveSolutionSourceAsync();
        Task SaveActiveDocumentAsync();
        Task SaveDocumentsAsync(DocumentSaveType type);
        Task<IEnumerable<string>> ListProjectDocumentsAsync();
    }

    [Export(typeof(IProjectSourceManager))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class ProjectSourceManager : IProjectSourceManager
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly IProject _project;

        [ImportingConstructor]
        public ProjectSourceManager(SVsServiceProvider serviceProvider, IProject project)
        {
            _serviceProvider = serviceProvider;
            _project = project;
        }

        public Task SaveDocumentsAsync(DocumentSaveType type)
        {
            switch (type)
            {
                case DocumentSaveType.ActiveDocument:
                    return SaveActiveDocumentAsync();
                case DocumentSaveType.OpenDocuments:
                    return SaveActiveSourceAsync();
                case DocumentSaveType.ProjectDocuments:
                    return SaveProjectSourceAsync();
                case DocumentSaveType.SolutionDocuments:
                default:
                    return Task.CompletedTask;
            }
        }

        public async Task SaveProjectSourceAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            SaveProjectDocuments();
        }

        public async Task SaveSolutionSourceAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            SaveSolutionDocuments();
        }

        public async Task SaveActiveDocumentAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            SaveActiveDocument();
        }

        public async Task SaveActiveSourceAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            _ = GetDTE().ItemOperations.PromptToSave;
        }

        public async Task<IEnumerable<string>> ListProjectDocumentsAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();

            var dte = GetDTE();
            var result = new List<string>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
                foreach (ProjectItem item in project.ProjectItems)
                    ListProjectDocumentsRecursively(item, result);

            return result;
        }

        private void ListProjectDocumentsRecursively(ProjectItem projItem, List<string> documents)
        {
            if (projItem.ProjectItems != null)
                foreach (ProjectItem item in projItem.ProjectItems)
                    ListProjectDocumentsRecursively(item, documents);
            if (projItem.Document != null)
                documents.Add(_project.GetRelativePath(projItem.Document.FullName));
        }

        private void SaveSolutionDocuments()
        {
            var dte = GetDTE();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
                foreach (ProjectItem item in project.ProjectItems)
                    SaveDocumentsRecursively(item);
        }

        private void SaveProjectDocuments()
        {
            var dte = GetDTE();

            if (dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
            {
                var activeProject = activeSolutionProjects.GetValue(0) as EnvDTE.Project;

                foreach (ProjectItem item in activeProject.ProjectItems)
                    SaveDocumentsRecursively(item);
            }
        }

        private void SaveDocumentsRecursively(ProjectItem projectItem)
        {
            if (projectItem.Document != null)
                projectItem.Document.Save();

            if (projectItem.ProjectItems != null)
                foreach (ProjectItem subItem in projectItem.ProjectItems)
                    SaveDocumentsRecursively(subItem);
        }

        private void SaveActiveDocument()
        {
            var dte = GetDTE();
            dte.ActiveDocument?.Save();
        }

        private DTE GetDTE()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);
            return dte;
        }
    }
}
