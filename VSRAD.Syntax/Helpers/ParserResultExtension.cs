using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Helpers
{
    internal static class ParserResultExtension
    {
        public static SnapshotSpan GetActualScope(this IBlock block, ITextSnapshot snapshot)
        {
            var start = block.TokenStart.GetStart(snapshot);
            var end = block.TokenEnd.GetEnd(snapshot);

            return new SnapshotSpan(snapshot, start, end - start);
        }

        public static IBlock GetBlockBy(this IReadOnlyList<IBlock> blocks, SnapshotPoint point)
        {
            foreach (var block in blocks)
            {
                if (block.Type == BlockType.Comment || block.Type == BlockType.Root)
                    continue;

                if (block.GetActualScope(point.Snapshot).Contains(point))
                    return block;
            }

            return blocks[0];
        }

        public static IBlock GetBlockBy(this IReadOnlyList<IBlock> blocks, AnalysisToken analysisToken)
        {
            foreach (var block in blocks)
            {
                if (block.Type == BlockType.Comment)
                    continue;

                if (block.Tokens.Contains(analysisToken))
                    return block;
            }

            return blocks[0];
        }

        public static IEnumerable<FunctionBlock> GetFunctions(this IReadOnlyList<IBlock> blocks) =>
            blocks.Where(b => b.Type == BlockType.Function).Cast<FunctionBlock>();

        public static IEnumerable<AnalysisToken> GetGlobalTokens(this IReadOnlyList<IBlock> blocks)
        {
            if (blocks.Count == 0)
                return Enumerable.Empty<AnalysisToken>();

            var globalTokens = blocks
                .Where(b => b.Type == BlockType.Function)
                .Cast<FunctionBlock>()
                .Select(fb => fb.Name)
                .ToList();
            var rootBlock = blocks[0];

            globalTokens.AddRange(rootBlock.Tokens.Where(t => t.Type == RadAsmTokenType.Label || t.Type == RadAsmTokenType.GlobalVariable));
            return globalTokens;
        }

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
