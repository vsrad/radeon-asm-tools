using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Composition;

namespace VSRAD.Package.ProjectSystem
{
    public delegate void OnSourceFileChange(string projectRelativePath);

    public interface IProjectEvents
    {
        event OnSourceFileChange SourceFileChanged;
    }

    [Export(typeof(IProjectEvents))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ProjectEvents : IProjectEvents
    {
        public event OnSourceFileChange SourceFileChanged;

        private readonly SVsServiceProvider _serviceProvider;

        // We need to keep references to Events and DocumentEvents, otherwise they'll be garbage collected
        // along with the event handler.
        private Events _events;
        private DocumentEvents _documentEvents;

        [ImportingConstructor]
        public ProjectEvents(SVsServiceProvider serviceProvider, IProject project)
        {
            _serviceProvider = serviceProvider;
            project.Loaded += InitializeEvents;
        }

        public void InitializeEvents(Options.ProjectOptions _)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(dte);

            _events = dte.Events;
            _documentEvents = _events.DocumentEvents;
            _documentEvents.DocumentSaved += OnProjectDocumentSaved;
        }

        private void OnProjectDocumentSaved(Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SourceFileChanged(document.FullName);
        }
    }
}
