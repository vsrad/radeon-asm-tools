using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Blocks
{
    internal class FunctionBlock : Block
    {
        public DefinitionToken Name { get; }
        public List<DefinitionToken> Parameters { get; }

        public FunctionBlock(IBlock parent, BlockType type, TrackingToken start, ITextSnapshot textSnapshot, DefinitionToken name) 
            : base(parent, type, start, textSnapshot)
        {
            Name = name;
            Parameters = new List<DefinitionToken>();
            parent.AddToken(name);
        }

        public override void AddToken(IAnalysisToken token)
        {
            base.AddToken(token);
            if (token.Type == RadAsmTokenType.FunctionParameter)
                Parameters.Add(token as DefinitionToken);
        }

        public override bool InRange(int point) =>
            Name.Span.End <= point && Area.End >= point;

        public override bool InRange(Span span) =>
            Name.Span.End <= span.Start && Area.End >= span.End;
    }
}
