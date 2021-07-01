using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public interface IAnalysisResult
    {
        IBlock Root { get; }
        IReadOnlyList<IBlock> Scopes { get; }
        IReadOnlyList<IErrorToken> Errors { get; }
        ITextSnapshot Snapshot { get; }
        AnalysisToken GetToken(int point);
        IBlock GetBlock(int point);
        IEnumerable<DefinitionToken> GetGlobalDefinitions();
    }
}
