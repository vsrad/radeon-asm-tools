using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Guide
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class IndentGuideFactory : DisposableProvider<IDocument, IndentGuide>, IWpfTextViewCreationListener
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly GeneralOptionProvider _generalOptionProvider;

        [ImportingConstructor]
        public IndentGuideFactory(IDocumentFactory documentFactory, GeneralOptionProvider generalOptionProvider)
        {
            _documentFactory = documentFactory;
            _generalOptionProvider = generalOptionProvider;
            _documentFactory.DocumentDisposed += DisposeRequest;
        }

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(Constants.IndentGuideAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.DifferenceChanges, Before = PredefinedAdornmentLayers.Text)]
        [TextViewRole(PredefinedTextViewRoles.Document)]
        public AdornmentLayerDefinition EditorAdornmentLayer = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            var document = _documentFactory.GetOrCreateDocument(textView.TextBuffer);
            if (document == null) return;

            GetValue(document, () => new IndentGuide(textView, document.DocumentAnalysis, _generalOptionProvider));
        }
    }
}