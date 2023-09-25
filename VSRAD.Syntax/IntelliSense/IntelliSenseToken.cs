using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense
{
    public sealed class IntelliSenseToken
    {
        /// <summary>
        /// Symbol for which IntelliSense information is requested.
        /// </summary>
        public AnalysisToken Symbol { get; }

        /// <summary>
        /// Navigable definitions of the symbol. Non-empty for instructions and symbols defined in source files, empty for builtins.
        /// </summary>
        public IReadOnlyList<NavigationToken> Definitions { get; }

        /// <summary>
        /// Documentation for a built-in function. Null if the symbol does not refer to a builtin.
        /// </summary>
        public BuiltinInfo BuiltinInfo { get; }

        public IntelliSenseToken(AnalysisToken symbol, IReadOnlyList<NavigationToken> definitions, BuiltinInfo builtinInfo)
        {
            Symbol = symbol;
            Definitions = definitions;
            BuiltinInfo = builtinInfo;
        }
    }
}
