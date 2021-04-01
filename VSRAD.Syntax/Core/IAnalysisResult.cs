using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public interface IAnalysisResult
    {
        IDocument Document { get; }
        IBlock Root { get; }
        IReadOnlyList<IBlock> Scopes { get; }
        IReadOnlyList<IErrorToken> Errors { get; }
        IReadOnlyList<IDocument> Includes { get; }
        ITextSnapshot Snapshot { get; }
        IAnalysisToken GetToken(int point);
        IBlock GetBlock(int point);
        IEnumerable<DefinitionToken> GetGlobalDefinitions();
    }
}
