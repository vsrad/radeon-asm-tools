using VSRAD.Syntax.Parser.Tokens;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VSRAD.Syntax.Parser.Blocks
{
    public class FunctionBlock : BaseBlock
    {
        public IBaseToken FunctionToken { get; }
        public override IList<IBaseToken> Tokens => base.Tokens;

        public FunctionBlock(
            IBaseBlock parrent,
            SnapshotPoint blockStart,
            IBaseToken functionToken,
            int spaceStart) : base(parrent, BlockType.Function, blockStart, spaceStart: spaceStart)
        {
            this.FunctionToken = functionToken;
            this.Tokens.Add(FunctionToken);
        }

        public override void SetActualSpan()
        {
            BlockActualSpan = new SnapshotSpan(FunctionToken.SymbolSpan.Start, BlockSpan.End);
        }
    }
}
