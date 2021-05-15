using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
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
        void SaveProjectState();
        void SaveDocuments(DocumentSaveType type);
        Task<IEnumerable<(string absolutePath, string relativePath)>> ListProjectFilesAsync();
    }

    [Export(typeof(IProjectSourceManager))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ProjectSourceManager : IProjectSourceManager
    {
        private readonly IProject _project;
        private readonly SVsServiceProvider _serviceProvider;

        public string ProjectRoot { get; }

        [ImportingConstructor]
        public ProjectSourceManager(IProject project, SVsServiceProvider serviceProvider)
        {
            _project = project;
            _serviceProvider = serviceProvider;
            ProjectRoot = Path.GetDirectoryName(project.UnconfiguredProject.FullPath);
        }

        public void SaveProjectState()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _project.SaveOptions();
            if (_project.Options.DebuggerOptions.Autosave)
                SaveDocuments(DocumentSaveType.OpenDocuments);
        }

        public void SaveDocuments(DocumentSaveType type)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);
            switch (type)
            {
                case DocumentSaveType.ActiveDocument:
                    if (dte.ActiveDocument?.Saved == false)
                        dte.ActiveDocument.Save();
                    break;
                case DocumentSaveType.OpenDocuments:
                    var documentTable = new RunningDocumentTable(_serviceProvider);
                    var vsDocumentTable = _serviceProvider.GetService(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
                    Assumes.Present(vsDocumentTable);

                    foreach (var document in documentTable)
                        // save only files in the tabs
                        if ((document.Flags & (uint)(_VSRDTFLAGS.RDT_VirtualDocument | _VSRDTFLAGS.RDT_CantSave)) == 0)
                            vsDocumentTable.SaveDocuments(
                                    (uint)__VSRDTSAVEOPTIONS.RDTSAVEOPT_SaveIfDirty,
                                    document.Hierarchy,
                                    document.ItemId,
                                    document.DocCookie);
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
            var configuredProject = _project.UnconfiguredProject.Services.ActiveConfiguredProjectProvider.ActiveConfiguredProject;
            var projectItems = await configuredProject.Services.SourceItems.GetItemsAsync();

            var files = new List<(string absolutePath, string relativePath)>();
            foreach (var item in projectItems)
            {
                string name;
                if (item.EvaluatedIncludeAsFullPath.StartsWith(ProjectRoot, StringComparison.Ordinal))
                {
                    name = item.EvaluatedIncludeAsRelativePath;
                }
                else
                {
                    name = await item.Metadata.GetEvaluatedPropertyValueAsync("Link"); // CPS-style links, used in RADProject
                    if (string.IsNullOrEmpty(name))
                        name = Path.GetFileName(item.EvaluatedIncludeAsFullPath); // VisualC-style links (project-relative Include starting with "..")
                }

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
