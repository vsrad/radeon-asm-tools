using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    internal class NavigableSymbol : INavigableSymbol
    {
        private readonly Action _navigateAction;

        public SnapshotSpan SymbolSpan { get; }
        public IEnumerable<INavigableRelationship> Relationships =>
            new List<INavigableRelationship>() { PredefinedNavigableRelationships.Definition };

        public NavigableSymbol(SnapshotSpan span, Action navigateAction)
        {
            SymbolSpan = span;
            _navigateAction = navigateAction;
        }

        public void Navigate(INavigableRelationship relationship) =>
            _navigateAction.Invoke();
    }
}
