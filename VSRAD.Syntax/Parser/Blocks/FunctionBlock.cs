﻿using VSRAD.Syntax.Parser.Tokens;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;

namespace VSRAD.Syntax.Parser.Blocks
{
    public class FunctionBlock : BaseBlock
    {
        public FunctionToken FunctionToken { get; }

        public FunctionBlock(
            IBaseBlock parrent,
            SnapshotPoint blockStart,
            FunctionToken functionToken,
            int spaceStart) : base(parrent, BlockType.Function, blockStart, spaceStart: spaceStart)
        {
            this.FunctionToken = functionToken;
            this.Tokens.Add(FunctionToken);
        }

        public override void SetActualSpan()
        {
            BlockActualSpan = new SnapshotSpan(FunctionToken.SymbolSpan.Start, BlockSpan.End);
        }

        public IEnumerable<IBaseToken> GetArgumentTokens() =>
            Tokens.Where(token => token.TokenType == TokenType.Argument);
    }
}
