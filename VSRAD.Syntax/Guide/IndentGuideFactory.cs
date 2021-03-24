using System.Collections.Generic;
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
        private readonly Dictionary<IDocument, IndentGuide> _guidesDictionary;

        [ImportingConstructor]
        public IndentGuideFactory(IDocumentFactory documentFactory, OptionsProvider optionsProvider)
        {
            _documentFactory = documentFactory;
            _optionsProvider = optionsProvider;
            _guidesDictionary = new Dictionary<IDocument, IndentGuide>();

            _documentFactory.DocumentDisposed += OnDocumentRemove;
        }

        private void OnDocumentRemove(IDocument document)
        {
            if (!_guidesDictionary.TryGetValue(document, out var guide)) return;
            guide.OnDestroy();
            _guidesDictionary.Remove(document);
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

            var guide = new IndentGuide(textView, document.DocumentAnalysis, _optionsProvider);
            _guidesDictionary.Add(document, guide);
        }
    }
}