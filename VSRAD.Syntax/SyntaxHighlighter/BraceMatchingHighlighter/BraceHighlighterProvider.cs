﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.IntelliSense;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.SyntaxHighlighter.BraceMatchingHighlighter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class BraceHighlighterProvider : IViewTaggerProvider
    {
        private readonly IDocumentFactory _documentFactory;
        
        [ImportingConstructor]
        public BraceHighlighterProvider(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            var document = _documentFactory.GetOrCreateDocument(buffer);
            return new BraceHighlighter(textView, buffer, document.DocumentTokenizer) as ITagger<T>;
        }
    }

}
