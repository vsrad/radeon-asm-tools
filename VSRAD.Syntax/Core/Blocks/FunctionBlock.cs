using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Blocks
{
    internal class FunctionBlock : Block
    {
        public DefinitionToken Name { get; }

        public FunctionBlock(IBlock parrent, BlockType type, TrackingToken tokenStart, DefinitionToken name) : base(parrent, type, tokenStart)
        {
            Name = name;
            parrent.AddToken(name);
        }
    }
}
