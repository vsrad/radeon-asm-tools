using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    public sealed class NavigationTokenServiceResult
    {
        public IReadOnlyList<INavigationToken> Values { get; }
        public IAnalysisToken ApplicableToken { get; }

        public NavigationTokenServiceResult(IReadOnlyList<INavigationToken> values, IAnalysisToken token)
        {
            Values = values;
            ApplicableToken = token;
        }
    }
}
