using Microsoft.VisualStudio.Language.Intellisense;
using System.Collections.Generic;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekableItem : IPeekableItem
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly INavigationToken _navigationToken;

        public PeekableItem(IPeekResultFactory peekResultFactory, INavigationToken navigationToken)
        {
            _peekResultFactory = peekResultFactory;
            _navigationToken = navigationToken;
        }

        public string DisplayName => null;

        public IEnumerable<IPeekRelationship> Relationships =>
            new List<IPeekRelationship>() { PredefinedPeekRelationships.Definitions };

        public IPeekResultSource GetOrCreateResultSource(string relationshipName) =>
            new PeekResultSource(_peekResultFactory, _navigationToken);
    }
}