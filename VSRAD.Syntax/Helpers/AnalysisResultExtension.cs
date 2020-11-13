using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Blocks;

namespace VSRAD.Syntax.Helpers
{
    internal static class AnalysisResultExtension
    {
        private static bool FInRange(this IBlock b, int point) => b.Type == BlockType.Function && b.InRange(point);

        private static FunctionBlock InnerInRange(IEnumerable<IBlock> blocks, int point) =>
            (FunctionBlock)blocks.FirstOrDefault(c => c.FInRange(point));


        public static FunctionBlock TryGetFunctionBlock(this IAnalysisResult analysisResult, SnapshotPoint point)
        {
            if (point.Snapshot != analysisResult.Snapshot) return null;
            
            var block = analysisResult.Scopes.FirstOrDefault(t => t.FInRange(point));
            if (block == null) return null;

            while (true)
            {
                var innerBlock = InnerInRange(block.Children, point);
                if (innerBlock != null)
                {
                    block = innerBlock;
                    continue;
                }

                break;
            }

            return (FunctionBlock)block;
        }
    }
}
