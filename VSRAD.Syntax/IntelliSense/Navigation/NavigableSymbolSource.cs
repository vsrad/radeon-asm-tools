﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.SyntaxHighlighter;

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

            return (navigableToken == AnalysisToken.Empty)
                ? Task.FromResult<INavigableSymbol>(null)
                : Task.FromResult<INavigableSymbol>(new NavigableSymbol(extent.Span, new SnapshotPoint(triggerSpan.Snapshot, navigableToken.TrackingToken.GetEnd(triggerSpan.Snapshot)), _navigationService));
        }

        public void Dispose()
        {
        }
    }
}
