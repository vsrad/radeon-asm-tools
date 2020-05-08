using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekableItemSource : IPeekableItemSource
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly NavigationTokenService _navigationTokenService;

        public PeekableItemSource(
            IPeekResultFactory peekResultFactory,
            NavigationTokenService definitionService)
        {
            _peekResultFactory = peekResultFactory ?? throw new ArgumentNullException(nameof(peekResultFactory));
            _navigationTokenService = definitionService ?? throw new ArgumentNullException(nameof(peekResultFactory));
        }

        public void AugmentPeekSession(IPeekSession session, IList<IPeekableItem> peekableItems)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (peekableItems == null)
                throw new ArgumentNullException(nameof(peekableItems));

            if (!string.Equals(session.RelationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase))
                return;

            var item = GetPeekableItem((IWpfTextView)session.TextView, _peekResultFactory);
            if (item != null)
                peekableItems.Add(item);
        }

        private IPeekableItem GetPeekableItem(IWpfTextView view, IPeekResultFactory peekResultFactory)
        {
            if (view != null)
            {
                var extent = NavigationTokenService.GetTextExtentOnCursor(view);
                var token = _navigationTokenService.GetNaviationItem(view, extent, false);
                if (token != null)
                    return new PeekableItem(peekResultFactory, token);
            }
            return null;
        }

        public void Dispose()
        {
        }
    }
}