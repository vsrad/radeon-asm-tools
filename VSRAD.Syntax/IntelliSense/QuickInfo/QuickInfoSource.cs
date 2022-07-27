using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VSRAD.Syntax.IntelliSense.QuickInfo
{
    internal class QuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly INavigationTokenService _navigationTokenService;
        private readonly IIntellisenseDescriptionBuilder _descriptionBuilder;
        private readonly ITextBuffer _textBuffer;

        public QuickInfoSource(ITextBuffer textBuffer, 
            INavigationTokenService navigationTokenService, 
            IIntellisenseDescriptionBuilder descriptionBuilder)
        {
            _textBuffer = textBuffer;
            _navigationTokenService = navigationTokenService;
            _descriptionBuilder = descriptionBuilder;
        }

        public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            var snapshot = _textBuffer.CurrentSnapshot;
            var triggerPoint = session.GetTriggerPoint(snapshot);
            if (!triggerPoint.HasValue) return null;

            var navigationsResult = await _navigationTokenService.GetNavigationsAsync(triggerPoint.Value);
            if (navigationsResult == null) return null;

            var dataElement = await _descriptionBuilder.GetColorizedDescriptionAsync(navigationsResult.Values, cancellationToken);
            if (dataElement == null) return null;

            var tokenSpan = navigationsResult.ApplicableToken.Span;
            var applicableSpan = snapshot.CreateTrackingSpan(tokenSpan, SpanTrackingMode.EdgeInclusive);

            return new QuickInfoItem(applicableSpan, dataElement);
        }

        public void Dispose()
        {
        }
    }
}
