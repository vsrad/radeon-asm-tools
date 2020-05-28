using VSRAD.Syntax.Parser.Tokens;
using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekableItem : IPeekableItem
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly AnalysisToken _token;
        private readonly ITextSnapshot _version;

        public PeekableItem(IPeekResultFactory peekResultFactory, 
            ITextDocumentFactoryService textDocumentFactory, 
            ITextSnapshot version, 
            AnalysisToken token)
        {
            _token = token;
            _version = version;
            _peekResultFactory = peekResultFactory;
            _textDocumentFactory = textDocumentFactory;
        }

        public string DisplayName => null;

        public IEnumerable<IPeekRelationship> Relationships =>
            new List<IPeekRelationship>() { PredefinedPeekRelationships.Definitions };

        public IPeekResultSource GetOrCreateResultSource(string relationshipName) =>
            new PeekResultSource(_peekResultFactory, _textDocumentFactory, _version, _token);
    }
}