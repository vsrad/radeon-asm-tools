using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Blocks
{
    internal class InstructionDocBlock : Block
    {
        public AnalysisToken DocComment { get; }
        public List<AnalysisToken> InstructionNames { get; } = new List<AnalysisToken>();

        public InstructionDocBlock(IBlock parent, AnalysisToken docComment) : base(parent, BlockType.InstructionDoc, docComment.TrackingToken)
        {
            DocComment = docComment;
        }

        public override void AddToken(AnalysisToken token)
        {
            base.AddToken(token);
            if (token.Type == RadAsmTokenType.Instruction)
                InstructionNames.Add(token);
        }
    }
}
