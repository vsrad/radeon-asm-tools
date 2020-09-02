using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Helpers
{
    internal static class ParserResultExtension
    {
        public static IEnumerable<FunctionBlock> GetFunctions(this IReadOnlyList<IBlock> blocks) =>
            blocks.Where(b => b.Type == BlockType.Function).Cast<FunctionBlock>();

        public static IEnumerable<AnalysisToken> GetScopedTokens(this IBlock block, RadAsmTokenType type)
        {
            var currentBlock = block;
            var scopedTokens = new List<AnalysisToken>();

            while (currentBlock != null)
            {
                scopedTokens.AddRange(currentBlock.Tokens.Where(t => t.Type == type));
                currentBlock = currentBlock.Parrent;
            }

            return scopedTokens;
        }

        public static IEnumerable<AnalysisToken> GetDefinitionToken(this IEnumerable<AnalysisToken> tokens) =>
            tokens.Where(t => t.Type == RadAsmTokenType.LocalVariable || t.Type == RadAsmTokenType.GlobalVariable || t.Type == RadAsmTokenType.Label);
    }
}
