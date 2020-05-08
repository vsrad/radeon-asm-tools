using VSRAD.Syntax.Parser.Tokens;
using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Collections.Generic;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekableItem : IPeekableItem
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly IBaseToken _token;

        public PeekableItem(IPeekResultFactory peekResultFactory, IBaseToken token)
        {
            _token = token;
            _peekResultFactory = peekResultFactory ?? throw new ArgumentNullException(nameof(peekResultFactory));
        }

        public string DisplayName => null;

        public IEnumerable<IPeekRelationship> Relationships =>
            new List<IPeekRelationship>() { PredefinedPeekRelationships.Definitions };

        public IPeekResultSource GetOrCreateResultSource(string relationshipName)
        {
            return new PeekResultSource(_peekResultFactory, _token);
        }
    }
}