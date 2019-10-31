using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.Guides
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class IndentGuideFactory : IWpfTextViewCreationListener
    {

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(Constants.IndentGuideAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
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
                ActivityLog.LogError(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
        }

        private IndentGuide InitializeIndentGuide(IWpfTextView textView)
        {
            var buffer = textView.TextBuffer;
            var parserManager = buffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());

            return new IndentGuide(textView, parserManager);
        }
    }
}