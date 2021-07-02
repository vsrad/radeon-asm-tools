using Microsoft.VisualStudio.Language.Intellisense;
using System.Collections.Generic;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekableItem : IPeekableItem
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly NavigationToken _navigationToken;

        public PeekableItem(IPeekResultFactory peekResultFactory, NavigationToken navigationToken)
        {
            _peekResultFactory = peekResultFactory;
            _navigationToken = navigationToken;
        }

        public string DisplayName => null;

        public IEnumerable<IPeekRelationship> Relationships =>
            new List<IPeekRelationship>() { PredefinedPeekRelationships.Definitions };

        public IPeekResultSource GetOrCreateResultSource(string relationshipName)
        {
            // TODO: is it possible to avoid this hack?
            // Visual Studio requires the view to be open in a window
            // before presenting the Peek Definition result.
            // Otherwise, user will see the error "the result cannot be viewed inline".
            Utils.OpenHiddenView(_navigationToken.Path);
            return new PeekResultSource(_peekResultFactory, _navigationToken);
        }
    }
}