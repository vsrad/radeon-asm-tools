using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    internal class NavigableSymbolSource : INavigableSymbolSource
    {
        private readonly INavigationTokenService _navigationService;

        public NavigableSymbolSource(INavigationTokenService navigationService)
        {
            _navigationService = navigationService;
        }

        public async Task<INavigableSymbol> GetNavigableSymbolAsync(SnapshotSpan triggerSpan, CancellationToken token)
        {
            var triggerPoint = triggerSpan.Start;
            var tokensResult = await _navigationService.GetNavigationsAsync(triggerPoint);
            if (tokensResult == null) return null;

            return new NavigableSymbol(tokensResult.ApplicableToken.Span,
                () => _navigationService.NavigateOrOpenNavigationList(tokensResult.Values));
        }

        public void Dispose() { }
    }
}
