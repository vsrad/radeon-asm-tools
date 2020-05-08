using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.IntelliSense;

namespace VSRAD.Syntax
{
    [Export]
    sealed class RadeonServiceProvider
    {
        [ImportingConstructor]
        public RadeonServiceProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public readonly IServiceProvider ServiceProvider;

        [Import]
        public ITextDocumentFactoryService TextDocumentFactoryService;

        [Import]
        public IFileExtensionRegistryService FileExtensionRegistryService;

        [Import]
        public IVsEditorAdaptersFactoryService EditorAdaptersFactoryService;

        [Import]
        public ITextSearchService2 TextSearchService;

        [Import]
        public IClassificationTypeRegistryService ClassificationTypeRegistryService;

        [Import]
        public IContentTypeRegistryService ContentTypeRegistryService;

        [Import]
        public IPeekBroker PeekBroker = null;

        [Import]
        public ISignatureHelpBroker SignatureHelpBroker = null;

        [Import]
        public IAsyncQuickInfoBroker QuickInfoBroker = null;
    }
}
