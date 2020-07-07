using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    internal class NavigableSymbol : INavigableSymbol
    {
        private readonly INavigationTokenService _navigationTokenService;
        private readonly IReadOnlyList<NavigationToken> _navigationTokens;

        public SnapshotSpan SymbolSpan { get; }
        public IEnumerable<INavigableRelationship> Relationships =>
            new List<INavigableRelationship>() { PredefinedNavigableRelationships.Definition };

        public NavigableSymbol(
            SnapshotSpan span,
            IReadOnlyList<NavigationToken> navigationTokens,
            INavigationTokenService navigationService)
        {
            SymbolSpan = span;
            _navigationTokens = navigationTokens;
            _navigationTokenService = navigationService;
        }

        public void Navigate(INavigableRelationship relationship)
        {
            try
            {
                _navigationTokenService.GoToPointOrOpenNavigationList(_navigationTokens);
            }
            catch (Exception e)
            {
                Error.ShowError(e, "Goto definition");
            }
        }
    }
}
