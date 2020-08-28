using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.Guide
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class IndentGuideFactory : IWpfTextViewCreationListener
    {
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        private readonly OptionsProvider _optionsProvider;

        [ImportingConstructor]
        public IndentGuideFactory(DocumentAnalysisProvoder documentAnalysisProvoder, OptionsProvider optionsProvider)
        {
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _optionsProvider = optionsProvider;
        }

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(Constants.IndentGuideAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.DifferenceChanges, Before = PredefinedAdornmentLayers.Text)]
        [TextViewRole(PredefinedTextViewRoles.Document)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            try
            {
                InitializeIndentGuide(textView);
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        private IndentGuide InitializeIndentGuide(IWpfTextView textView)
        {
            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(textView.TextBuffer);

            return new IndentGuide(textView, documentAnalysis, _optionsProvider);
        }
    }
}