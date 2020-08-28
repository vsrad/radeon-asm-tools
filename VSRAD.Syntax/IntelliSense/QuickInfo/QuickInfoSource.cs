using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense.QuickInfo
{
    internal class QuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly INavigationTokenService _navigationService;
        private readonly IIntellisenseDescriptionBuilder _descriptionBuilder;
        private readonly ITextBuffer _textBuffer;
        private readonly DocumentAnalysis _documentAnalysis;

        public QuickInfoSource(
            ITextBuffer textBuffer, 
            DocumentAnalysis documentAnalysis,
            INavigationTokenService navigationService,
            IIntellisenseDescriptionBuilder descriptionBuilder)
        {
            _textBuffer = textBuffer;
            _documentAnalysis = documentAnalysis;
            _navigationService = navigationService;
            _descriptionBuilder = descriptionBuilder;
        }

        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
                return Task.FromResult<QuickInfoItem>(null);

            var extent = triggerPoint.Value.GetExtent();

            var navigationTokens = _navigationService.GetNaviationItem(extent);
            if (navigationTokens.Count > 0)
            {
                var dataElement = _descriptionBuilder.GetColorizedDescription(navigationTokens);
                if (dataElement == null)
                    return Task.FromResult<QuickInfoItem>(null);

                var applicableToSpan = _documentAnalysis.CurrentSnapshot.CreateTrackingSpan(extent.Span.Start, extent.Span.Length, SpanTrackingMode.EdgeInclusive);
                return Task.FromResult(new QuickInfoItem(applicableToSpan, dataElement));
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        public void Dispose()
        {
        }
    }
}
