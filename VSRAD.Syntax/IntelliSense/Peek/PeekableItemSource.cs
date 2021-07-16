using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.IntelliSense.Navigation.NavigationList;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekableItemSource : IPeekableItemSource
    {
        private readonly ITextBuffer _textBuffer;
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly INavigationTokenService _navigationTokenService;

        public PeekableItemSource(ITextBuffer textBuffer, 
            IPeekResultFactory peekResultFactory, 
            INavigationTokenService navigationService)
        {
            _textBuffer = textBuffer;
            _peekResultFactory = peekResultFactory;
            _navigationTokenService = navigationService;
        }

        public void AugmentPeekSession(IPeekSession session, IList<IPeekableItem> peekableItems)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (peekableItems == null)
                throw new ArgumentNullException(nameof(peekableItems));

            if (!string.Equals(session.RelationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase))
                return;

            var triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue) return;

            var tokensResult = ThreadHelper.JoinableTaskFactory.Run(
                () => _navigationTokenService.GetNavigationsAsync(triggerPoint.Value));

            if (tokensResult == null || tokensResult.Values.Count == 0)
                return;

            if (tokensResult.Values.Count == 1)
            {
                var peekableToken = tokensResult.Values[0];
                var peekableItem = new PeekableItem(_peekResultFactory, peekableToken);
                peekableItems.Add(peekableItem);
            }
            else
            {
                NavigationList.UpdateNavigationList(tokensResult.Values);
            }
        }

        public void Dispose() { }
    }
}