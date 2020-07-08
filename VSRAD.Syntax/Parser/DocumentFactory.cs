using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.Parser
{
    internal interface IDocumentFactory
    {
        DocumentInfo GetDocument(string path);
        DocumentInfo GetDocument(ITextBuffer buffer);
    }

    [Export(typeof(IDocumentFactory))]
    internal class DocumentFactory : IDocumentFactory
    {
        private readonly RadeonServiceProvider _serviceProvider;
        private readonly ContentTypeManager _contentTypeManager;

        [ImportingConstructor]
        DocumentFactory(RadeonServiceProvider serviceProvider, ContentTypeManager contentTypeManager)
        {
            _serviceProvider = serviceProvider;
            _contentTypeManager = contentTypeManager;
        }

        public DocumentInfo GetDocument(string path)
        {
            if (Utils.IsDocumentOpen(_serviceProvider.ServiceProvider, path, out var vsTextBuffer))
            {
                var textBuffer = _serviceProvider.EditorAdaptersFactoryService.GetDataBuffer(vsTextBuffer);
                return GetDocument(textBuffer);
            }

            var contentType = _contentTypeManager.DetermineContentType(path);
            var textDocument = _serviceProvider.TextDocumentFactoryService.CreateAndLoadTextDocument(path, contentType);

            return GetOrCreateDocumentInfo(textDocument);
        }

        public DocumentInfo GetDocument(ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var textDocument))
                return GetOrCreateDocumentInfo(textDocument);

            throw new ArgumentException($"Cannot find {nameof(ITextDocument)} associated with {buffer}");
        }

        private DocumentInfo GetOrCreateDocumentInfo(ITextDocument document) =>
            document.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new DocumentInfo(document));
    }
}
