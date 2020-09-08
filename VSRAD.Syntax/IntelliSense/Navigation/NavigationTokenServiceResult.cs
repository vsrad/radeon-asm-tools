using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    public struct NavigationTokenServiceResult
    {
        public static NavigationTokenServiceResult Empty { get { return new NavigationTokenServiceResult(); } }

        public bool HasValue { get; }
        public IReadOnlyList<NavigationToken> Values { get; }
        public AnalysisToken ApplicableToken { get; }

        public NavigationTokenServiceResult(IReadOnlyList<NavigationToken> values, AnalysisToken token)
        {
            HasValue = true;
            Values = values;
            ApplicableToken = token;
        }
    }
}
