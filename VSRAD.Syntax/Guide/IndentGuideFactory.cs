using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.Guide
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class IndentGuideFactory : IWpfTextViewCreationListener
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly OptionsProvider _optionsProvider;

        [ImportingConstructor]
        public IndentGuideFactory(IDocumentFactory documentFactory, OptionsProvider optionsProvider)
        {
            _documentFactory = documentFactory;
            _optionsProvider = optionsProvider;
        }

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(Constants.IndentGuideAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.DifferenceChanges, Before = PredefinedAdornmentLayers.Text)]
        [TextViewRole(PredefinedTextViewRoles.Document)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            var document = _documentFactory.GetOrCreateDocument(textView.TextBuffer);
            if (document != null)
                textView.Properties.AddProperty(typeof(IndentGuide), new IndentGuide(textView, document.DocumentAnalysis, _optionsProvider));
        }
    }
}