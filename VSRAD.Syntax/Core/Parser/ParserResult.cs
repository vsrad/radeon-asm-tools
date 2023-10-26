using System.Collections.Generic;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Parser
{
    public sealed class ParserResult
    {
        public IReadOnlyList<IBlock> Blocks { get; }
        public IReadOnlyList<IErrorToken> Errors { get; }
        public IBlock RootBlock { get; }

        public ParserResult(IReadOnlyList<IBlock> blocks, IReadOnlyList<IErrorToken> errors)
        {
            Blocks = blocks;
            Errors = errors;
            RootBlock = blocks[0];
        }
    }
}
