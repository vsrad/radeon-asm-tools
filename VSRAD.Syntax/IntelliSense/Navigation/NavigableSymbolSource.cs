using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    internal class NavigableSymbolSource : INavigableSymbolSource
    {
        private readonly INavigationTokenService _navigationService;

        public NavigableSymbolSource(INavigationTokenService navigationService)
        {
            _navigationService = navigationService;
        }

        public Task<INavigableSymbol> GetNavigableSymbolAsync(SnapshotSpan triggerSpan, CancellationToken token)
        {
            var extent = triggerSpan.Start.GetExtent();
            var navigableTokens = _navigationService.GetNaviationItem(extent, true);

            return (navigableTokens.Count == 0)
                ? Task.FromResult<INavigableSymbol>(null)
                : Task.FromResult<INavigableSymbol>(new NavigableSymbol(extent.Span, navigableTokens, _navigationService));
        }

        public void Dispose()
        {
        }
    }
}
