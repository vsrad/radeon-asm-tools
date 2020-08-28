using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Blocks
{
    internal class FunctionBlock : Block
    {
        public AnalysisToken Name { get; }

        public FunctionBlock(IBlock parrent, BlockType type, TrackingToken tokenStart, AnalysisToken name) : base(parrent, type, tokenStart)
        {
            Name = name;
        }
    }
}
