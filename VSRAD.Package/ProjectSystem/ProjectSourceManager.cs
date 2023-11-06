using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using VSRAD.Package.Utils;

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
        IVsTextBuffer GetDocumentTextBufferByPath(string path);
        IEditorView GetActiveEditorView();
        Task<IEnumerable<(string absolutePath, string relativePath)>> ListProjectFilesAsync();
    }

    [Export(typeof(IProjectSourceManager))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ProjectSourceManager : IProjectSourceManager
    {
        public const string NoFilesOpenError = "No files open in the editor.";

        public string ProjectRoot { get; }

        private readonly IProject _project;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;

        private DTE2 _dte;
        private RunningDocumentTable _runningDocumentTable;
        private IVsRunningDocumentTable _vsRunningDocumentTable;
        private IVsTextManager2 _textManager;

        [ImportingConstructor]
        public ProjectSourceManager(IProject project, ITextDocumentFactoryService textDocumentFactoryService, SVsServiceProvider serviceProvider)
        {
            ProjectRoot = Path.GetDirectoryName(project.UnconfiguredProject.FullPath);
            _project = project;
            _textDocumentFactoryService = textDocumentFactoryService;

            _project.RunWhenLoaded((_) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _dte = (DTE2)serviceProvider.GetService(typeof(DTE));
                _runningDocumentTable = new RunningDocumentTable(serviceProvider);
                _vsRunningDocumentTable = serviceProvider.GetService(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
                _textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            });
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
            switch (type)
            {
                case DocumentSaveType.ActiveDocument:
                    Assumes.Present(_dte);
                    if (_dte.ActiveDocument?.Saved == false)
                        _dte.ActiveDocument.Save();
                    break;
                case DocumentSaveType.OpenDocuments:
                    Assumes.Present(_runningDocumentTable);
                    Assumes.Present(_vsRunningDocumentTable);
                    foreach (var document in _runningDocumentTable)
                        // save only files in the tabs
                        if ((document.Flags & (uint)(_VSRDTFLAGS.RDT_VirtualDocument | _VSRDTFLAGS.RDT_CantSave)) == 0)
                            _vsRunningDocumentTable.SaveDocuments(
                                    (uint)__VSRDTSAVEOPTIONS.RDTSAVEOPT_SaveIfDirty,
                                    document.Hierarchy,
                                    document.ItemId,
                                    document.DocCookie);
                    break;
                case DocumentSaveType.ProjectDocuments:
                    Assumes.Present(_dte);
                    if (_dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
                        if (activeSolutionProjects.GetValue(0) is EnvDTE.Project activeProject)
                            foreach (ProjectItem item in activeProject.ProjectItems)
                                SaveDocumentsRecursively(item);
                    break;
                case DocumentSaveType.SolutionDocuments:
                    Assumes.Present(_dte);
                    foreach (EnvDTE.Project project in _dte.Solution.Projects)
                        foreach (ProjectItem item in project.ProjectItems)
                            SaveDocumentsRecursively(item);
                    break;
            }
        }

        public IVsTextBuffer GetDocumentTextBufferByPath(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Assumes.Present(_runningDocumentTable);
            return _runningDocumentTable.FindDocument(path) as IVsTextBuffer;
        }

        public IEditorView GetActiveEditorView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Assumes.Present(_textManager);
            _textManager.GetActiveView2(0, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out var activeView);
            return activeView != null ? new VsEditorView(activeView, _textDocumentFactoryService) : throw new InvalidOperationException(NoFilesOpenError);
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
