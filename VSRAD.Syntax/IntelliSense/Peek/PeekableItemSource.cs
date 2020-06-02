using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekableItemSource : IPeekableItemSource
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly NavigationTokenService _navigationTokenService;

        public PeekableItemSource(IPeekResultFactory peekResultFactory,
            ITextDocumentFactoryService textDocumentFactory,
            NavigationTokenService definitionService)
        {
            _peekResultFactory = peekResultFactory ?? throw new ArgumentNullException(nameof(peekResultFactory));
            _textDocumentFactory = textDocumentFactory ?? throw new ArgumentNullException(nameof(textDocumentFactory));
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

        private IPeekableItem GetPeekableItem(ITextView view, IPeekResultFactory peekResultFactory)
        {
            if (view != null)
            {
                var extent = NavigationTokenService.GetTextExtentOnCursor(view);
                var token = _navigationTokenService.GetNaviationItem(extent, true);
                if (token != NavigationToken.Empty)
                    return new PeekableItem(peekResultFactory, _textDocumentFactory, token.Snapshot, token.AnalysisToken);
            }
            return null;
        }

        public void Dispose()
        {
        }
    }
}