using VSRAD.Syntax.Parser.Tokens;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VSRAD.Syntax.Parser.Blocks
{
    public interface IBaseBlock
    {
        bool BlockReady { get; }
        SnapshotSpan BlockSpan { get; }
        SnapshotSpan BlockActualSpan { get; }
        IList<IBaseBlock> Children { get; }
        IList<IBaseToken> Tokens { get; }
        IBaseBlock Parrent { get; }
        BlockType BlockType { get; }

        IBaseBlock AddChildren(BlockType blockType, SnapshotPoint startBlock, SnapshotPoint blockActualStart = default);
        IBaseBlock AddChildren(IBaseBlock baseBlock);
        IBaseToken AddToken(SnapshotSpan symbolSpan, TokenType tokenType);
        void SetBlockReady(SnapshotPoint endBlock);
        IList<IBaseBlock> GetBlockElements();
        IList<SnapshotSpan> GetBlocksSnapshotSpan();
        IList<Span> GetBlocksSpan();
        IList<IBaseToken> GetTokens();
    }
}
