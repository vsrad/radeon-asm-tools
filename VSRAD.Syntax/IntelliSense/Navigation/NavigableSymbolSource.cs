using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    internal class NavigableSymbolSource : INavigableSymbolSource
    {
        private readonly NavigationTokenService _navigationService;

        public NavigableSymbolSource(NavigationTokenService navigationService)
        {
            _navigationService = navigationService;
        }

        public Task<INavigableSymbol> GetNavigableSymbolAsync(SnapshotSpan triggerSpan, CancellationToken token)
        {
            var extent = triggerSpan.Start.GetExtent();
            var navigableToken = _navigationService.GetNaviationItem(extent, false);

            return (navigableToken == null) 
                ? Task.FromResult<INavigableSymbol>(null) 
                : Task.FromResult<INavigableSymbol>(new NavigableSymbol(extent.Span, navigableToken.SymbolSpan.Start, _navigationService));
        }

        public void Dispose()
        {
        }
    }
}
