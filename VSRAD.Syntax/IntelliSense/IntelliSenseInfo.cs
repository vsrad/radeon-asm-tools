using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense
{
    public sealed class IntelliSenseInfo
    {
        /// <summary>
        /// Syntax type for which IntelliSense information is provided.
        /// </summary>
        public AsmType AsmType { get; }

        /// <summary>
        /// Symbol for which IntelliSense information is provided.
        /// </summary>
        public string Symbol { get; }

        /// <summary>
        /// Token type that corresponds to this symbol.
        /// </summary>
        public RadAsmTokenType SymbolType { get; }

        /// <summary>
        /// Non-null if IntelliSense information is requested for a symbol located in a source file (e.g. when requested by QuickInfo).
        /// Null if the symbol is not located in a source file (e.g. when requested by autocompletion).
        /// </summary>
        public SnapshotSpan? SymbolSpan { get; }

        /// <summary>
        /// Navigable definitions of the symbol. Non-empty for instructions and symbols defined in source files, empty for builtins.
        /// </summary>
        public IReadOnlyList<NavigationToken> Definitions { get; }

        /// <summary>
        /// Documentation for a built-in function. Null if the symbol does not refer to a builtin.
        /// </summary>
        public BuiltinInfo BuiltinInfo { get; }

        public IntelliSenseInfo(AsmType asmType, string symbol, RadAsmTokenType symbolType, SnapshotSpan? symbolSpan, IReadOnlyList<NavigationToken> definitions, BuiltinInfo builtinInfo)
        {
            AsmType = asmType;
            Symbol = symbol;
            SymbolType = symbolType;
            SymbolSpan = symbolSpan;
            Definitions = definitions;
            BuiltinInfo = builtinInfo;
        }
    }
}
