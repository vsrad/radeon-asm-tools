using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Blocks
{
    public interface IFunctionBlock : IBlock
    {
        IFunctionToken Name { get; }
        IList<IDefinitionToken> Parameters { get; }
    }

    internal class FunctionBlock : Block, IFunctionBlock
    {
        public IFunctionToken Name { get; }
        public IList<IDefinitionToken> Parameters { get; }

        public FunctionBlock(IBlock parent, BlockType type, TrackingToken start, ITextSnapshot textSnapshot, FunctionToken name) 
            : base(parent, type, start, textSnapshot)
        {
            Name = name;
            Parameters = new List<IDefinitionToken>();
            parent.AddToken(name);
            name.FunctionBlock = this;
        }

        public override void AddToken(IAnalysisToken token)
        {
            base.AddToken(token);
            if (token.Type == RadAsmTokenType.FunctionParameter)
                Parameters.Add(token as IDefinitionToken);
        }

        public override bool InRange(int point) =>
            Name.Span.End <= point && Area.End >= point;

        public override bool InRange(Span span) =>
            Name.Span.End <= span.Start && Area.End >= span.End;
    }
}
