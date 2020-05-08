using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense.QuickInfo
{
    internal class QuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly NavigationTokenService _definitionService;
        private readonly ITextBuffer _textBuffer;

        public QuickInfoSource(ITextBuffer textBuffer, NavigationTokenService definitionService)
        {
            _textBuffer = textBuffer;
            _definitionService = definitionService;
        }

        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
                return Task.FromResult<QuickInfoItem>(null);

            var currentSnapshot = _textBuffer.CurrentSnapshot;
            var extent = triggerPoint.Value.GetExtent();

            var navigationToken = _definitionService.GetNaviationItem(extent);
            if (navigationToken != null)
            {
                var dataElement = IntellisenseTokenDescription.GetColorizedDescription(navigationToken);
                if (dataElement == null)
                    return Task.FromResult<QuickInfoItem>(null);

                var applicableToSpan = currentSnapshot.CreateTrackingSpan(extent.Span.Start, navigationToken.TokenName.Length, SpanTrackingMode.EdgeInclusive);
                return Task.FromResult(new QuickInfoItem(applicableToSpan, dataElement));
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        public void Dispose()
        {
        }
    }
}
