using Microsoft.VisualStudio.Text;
using System;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense
{
    public interface INavigationService
    {
        void GoToDefinition(SnapshotPoint point);
        void GoToPoint(SnapshotPoint point);
    }

    internal class NavigationService : INavigationService
    {
        private readonly Lazy<IDocumentFactory> _documentFactory;
        private readonly Lazy<INavigationTokenService> _navigationTokenService;

        public NavigationService(Lazy<IDocumentFactory> documentFactory, Lazy<INavigationTokenService> navigationTokenService)
        {
            _documentFactory = documentFactory;
            _navigationTokenService = navigationTokenService;
        }

        public void GoToDefinition(SnapshotPoint point) => throw new NotImplementedException();

        public void GoToPoint(SnapshotPoint point)
        {
            var document = _documentFactory.Value.GetOrCreateDocument(point.Snapshot.TextBuffer);
            if (document != null)
                document.NavigateToPosition(point);
        }
    }
}
