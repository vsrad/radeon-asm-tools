using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekableItemSource : IPeekableItemSource
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly INavigationTokenService _navigationTokenService;

        public PeekableItemSource(IPeekResultFactory peekResultFactory,
            ITextDocumentFactoryService textDocumentFactory,
            INavigationTokenService definitionService)
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
                var extent = view.GetTextExtentOnCursor();
                var tokens = _navigationTokenService.GetNaviationItem(extent, true);
                if (tokens.Count == 1)
                    return new PeekableItem(peekResultFactory, _textDocumentFactory, tokens[0].Snapshot, tokens[0].AnalysisToken);
            }
            return null;
        }

        public void Dispose()
        {
        }
    }
}