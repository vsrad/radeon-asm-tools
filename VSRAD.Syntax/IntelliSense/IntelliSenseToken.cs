using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense
{
    public sealed class IntelliSenseToken
    {
        public AnalysisToken Symbol { get; }

        public IReadOnlyList<NavigationToken> Definitions { get; }

        public IntelliSenseToken(AnalysisToken symbol, IReadOnlyList<NavigationToken> definitions)
        {
            Symbol = symbol;
            Definitions = definitions;
        }
    }
}
