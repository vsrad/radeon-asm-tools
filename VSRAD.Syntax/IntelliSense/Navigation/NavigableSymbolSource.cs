using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    internal class NavigableSymbolSource : INavigableSymbolSource
    {
        private readonly IIntelliSenseService _intelliSenseService;

        public NavigableSymbolSource(IIntelliSenseService intelliSenseService)
        {
            _intelliSenseService = intelliSenseService;
        }

        public async Task<INavigableSymbol> GetNavigableSymbolAsync(SnapshotSpan triggerSpan, CancellationToken token)
        {
            var triggerPoint = triggerSpan.Start;
            var intelliSenseToken = await _intelliSenseService.GetIntelliSenseTokenAsync(triggerPoint);
            if (intelliSenseToken == null || intelliSenseToken.Definitions.Count == 0)
                return null;

            return new NavigableSymbol(intelliSenseToken.Symbol.Span,
                () => _intelliSenseService.NavigateOrOpenNavigationList(intelliSenseToken.Definitions));
        }

        public void Dispose() { }
    }
}
