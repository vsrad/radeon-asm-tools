using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VSRAD.Syntax.IntelliSense.QuickInfo
{
    internal class QuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly IIntelliSenseService _intelliSenseService;
        private readonly IIntelliSenseDescriptionBuilder _descriptionBuilder;
        private readonly ITextBuffer _textBuffer;

        public QuickInfoSource(ITextBuffer textBuffer,
            IIntelliSenseService intelliSenseService,
            IIntelliSenseDescriptionBuilder descriptionBuilder)
        {
            _textBuffer = textBuffer;
            _intelliSenseService = intelliSenseService;
            _descriptionBuilder = descriptionBuilder;
        }

        public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            var snapshot = _textBuffer.CurrentSnapshot;
            var triggerPoint = session.GetTriggerPoint(snapshot);
            if (!triggerPoint.HasValue) return null;

            var intelliSenseToken = await _intelliSenseService.GetIntelliSenseInfoAsync(triggerPoint.Value);
            if (intelliSenseToken == null || !intelliSenseToken.SymbolSpan.HasValue) return null;

            var dataElement = await _descriptionBuilder.GetDescriptionAsync(intelliSenseToken, cancellationToken);
            if (dataElement == null) return null;

            var applicableSpan = snapshot.CreateTrackingSpan(intelliSenseToken.SymbolSpan.Value, SpanTrackingMode.EdgeInclusive);
            return new QuickInfoItem(applicableSpan, dataElement);
        }

        public void Dispose()
        {
        }
    }
}
