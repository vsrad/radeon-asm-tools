using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense.QuickInfo
{
    internal class QuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly NavigationTokenService _navigationService;
        private readonly ITextBuffer _textBuffer;
        private readonly DocumentAnalysis _documentAnalysis;

        public QuickInfoSource(ITextBuffer textBuffer, 
            DocumentAnalysis documentAnalysis,
            NavigationTokenService navigationService)
        {
            _textBuffer = textBuffer;
            _documentAnalysis = documentAnalysis;
            _navigationService = navigationService;
        }

        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
                return Task.FromResult<QuickInfoItem>(null);

            var extent = triggerPoint.Value.GetExtent();

            var navigationToken = _navigationService.GetNaviationItem(extent).AnalysisToken;
            if (navigationToken != AnalysisToken.Empty)
            {
                var dataElement = IntellisenseTokenDescription.GetColorizedTokenDescription(_documentAnalysis, navigationToken);
                if (dataElement == null)
                    return Task.FromResult<QuickInfoItem>(null);

                var applicableToSpan = _documentAnalysis.CurrentSnapshot.CreateTrackingSpan(extent.Span.Start, navigationToken.TrackingToken.Length, SpanTrackingMode.EdgeInclusive);
                return Task.FromResult(new QuickInfoItem(applicableToSpan, dataElement));
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        public void Dispose()
        {
        }
    }
}
