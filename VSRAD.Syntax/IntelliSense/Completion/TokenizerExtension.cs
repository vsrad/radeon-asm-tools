using System.Collections.Generic;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal static class TokenizerExtension
    {
        public static bool IsDismissToken(this IDocumentTokenizer documentTokenizer, int tokenId)
        {
            if (documentTokenizer.CurrentResult == null)
                return false;

            var asmType = documentTokenizer.CurrentResult.Snapshot.GetAsmType();
            return _dismissToken[asmType].Contains(tokenId);
        }

        private static readonly Dictionary<AsmType, ISet<int>> _dismissToken = new Dictionary<AsmType, ISet<int>>()
        {
            {
                AsmType.RadAsm,
                new HashSet<int>
                {
                    RadAsmLexer.MACRO,
                    RadAsmLexer.SET
                }
            },
            {
                AsmType.RadAsm2,
                new HashSet<int>
                {
                    RadAsm2Lexer.FUNCTION,
                    RadAsm2Lexer.VAR,
                    RadAsm2Lexer.LABEL
                }
            },
            {
                AsmType.RadAsmDoc,
                new HashSet<int>
                {
                    RadAsmDocLexer.LET
                }
            }
        };
    }
}
