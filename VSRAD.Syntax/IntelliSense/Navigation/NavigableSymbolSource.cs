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
            var intelliSenseToken = await _intelliSenseService.GetIntelliSenseInfoAsync(triggerPoint);
            if (intelliSenseToken == null || !intelliSenseToken.SymbolSpan.HasValue || intelliSenseToken.Definitions.Count == 0 )
                return null;

            return new NavigableSymbol(intelliSenseToken.SymbolSpan.Value,
                () => _intelliSenseService.NavigateOrOpenNavigationList(intelliSenseToken.Definitions));
        }

        public void Dispose() { }
    }
}
