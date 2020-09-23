using Microsoft.VisualStudio.Text;
using System.Linq;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Blocks;

namespace VSRAD.Syntax.Helpers
{
    internal static class AnalysisResultExtension
    {
        public static FunctionBlock TryGetFunctionBlock(this IAnalysisResult analysisResult, SnapshotPoint point) =>
            point.Snapshot == analysisResult.Snapshot
                ? (FunctionBlock)analysisResult.Scopes.FirstOrDefault(t => t.Type == BlockType.Function && t.InRange(point))
                : null;
    }
}
