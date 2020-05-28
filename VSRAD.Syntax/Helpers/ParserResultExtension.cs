using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.Helpers
{
    internal static class ParserResultExtension
    {
        public static IBlock GetBlockBy(this IReadOnlyList<IBlock> blocks, SnapshotPoint point)
        {
            foreach (var block in blocks)
            {
                if (block.Type == BlockType.Comment || block.Type == BlockType.Root)
                    continue;

                if (block.Scope.GetSpan(point.Snapshot).Contains(point))
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
    }
}
