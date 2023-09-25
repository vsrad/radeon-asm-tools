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
        private readonly IIntelliSenseService _intelliSenseService;

        public PeekableItemSource(ITextBuffer textBuffer, 
            IPeekResultFactory peekResultFactory, 
            IIntelliSenseService intelliSenseService)
        {
            _textBuffer = textBuffer;
            _peekResultFactory = peekResultFactory;
            _intelliSenseService = intelliSenseService;
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

            var intelliSenseToken = ThreadHelper.JoinableTaskFactory.Run(
                () => _intelliSenseService.GetIntelliSenseTokenAsync(triggerPoint.Value));
            if (intelliSenseToken == null || intelliSenseToken.Definitions.Count == 0)
                return;

            if (intelliSenseToken.Definitions.Count == 1)
            {
                var peekableToken = intelliSenseToken.Definitions[0];
                var peekableItem = new PeekableItem(_peekResultFactory, peekableToken);
                peekableItems.Add(peekableItem);
            }
            else
            {
                NavigationList.UpdateNavigationList(intelliSenseToken.Definitions);
            }
        }

        public void Dispose() { }
    }
}