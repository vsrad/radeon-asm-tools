using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.Guide
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class IndentGuideFactory : IWpfTextViewCreationListener
    {
        [Import]
        private readonly Options.OptionsProvider _optionsProvider;

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
            var buffer = textView.TextBuffer;
            var parserManager = buffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());

            return new IndentGuide(textView, parserManager, _optionsProvider);
        }
    }
}