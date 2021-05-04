using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense.FindReferences
{
    internal class FindReferencesPresenter
    {
        private const string Label = "RadeonAsm Find All References";

        private readonly Lazy<IFindAllReferencesService> _referencesServiceLazy;
        private readonly List<TableDataSourceFindReferencesContext> _currentContexts =
            new List<TableDataSourceFindReferencesContext>();

        public readonly INavigationTokenService NavigationTokenService;
        private readonly Lazy<IDocumentFactory> _documentFactoryLazy;

        public FindReferencesPresenter(IServiceProvider serviceProvider, Lazy<IDocumentFactory> documentFactoryLazy, INavigationTokenService navigationTokenService)
        {
            _referencesServiceLazy = new Lazy<IFindAllReferencesService>(() =>
                (IFindAllReferencesService)serviceProvider.GetService(typeof(SVsFindAllReferences)));
            _documentFactoryLazy = documentFactoryLazy;

            NavigationTokenService = navigationTokenService;
        }

        public void TryFindAllReferences(SnapshotPoint bufferPosition)
        {
            var referenceService = _referencesServiceLazy.Value;
            var window = referenceService.StartSearch(Label);

            StartSearchWithReferences(bufferPosition, window);
        }

        public void Add(TableDataSourceFindReferencesContext ctx) =>
            _currentContexts.Add(ctx);

        public void Remove(TableDataSourceFindReferencesContext ctx) => 
            _currentContexts.Remove(ctx);

        private TableDataSourceFindReferencesContext StartSearchWithReferences(SnapshotPoint point, IFindAllReferencesWindow window) =>
            new TableDataSourceFindReferencesContext(this, _documentFactoryLazy.Value, point, window);
    }
}
