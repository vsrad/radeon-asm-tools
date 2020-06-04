using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    internal class NavigableSymbol : INavigableSymbol
    {
        private readonly NavigationTokenService _navigationTokenService;
        private readonly SnapshotPoint _navigationPoint;

        public SnapshotSpan SymbolSpan { get; }
        public IEnumerable<INavigableRelationship> Relationships =>
            new List<INavigableRelationship>() { PredefinedNavigableRelationships.Definition };

        public NavigableSymbol(
            SnapshotSpan span,
            SnapshotPoint navigationPoint,
            NavigationTokenService navigationService)
        {
            SymbolSpan = span;
            _navigationPoint = navigationPoint;
            _navigationTokenService = navigationService;
        }

        public void Navigate(INavigableRelationship relationship)
        {
            try
            {
                _navigationTokenService.GoToPoint(_navigationPoint);
            }
            catch (Exception e)
            {
                Error.ShowError(e, "Goto definition");
            }
        }
    }
}
