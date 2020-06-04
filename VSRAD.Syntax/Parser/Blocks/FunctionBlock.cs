using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.Parser.Blocks
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
