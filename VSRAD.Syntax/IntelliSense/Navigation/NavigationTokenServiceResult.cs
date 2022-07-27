using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    public sealed class NavigationTokenServiceResult
    {
        public IReadOnlyList<NavigationToken> Values { get; }
        public AnalysisToken ApplicableToken { get; }

        public NavigationTokenServiceResult(IReadOnlyList<NavigationToken> values, AnalysisToken token)
        {
            Values = values;
            ApplicableToken = token;
        }
    }
}
