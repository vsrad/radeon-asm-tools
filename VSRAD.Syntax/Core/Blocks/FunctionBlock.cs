using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Blocks
{
    internal class FunctionBlock : Block
    {
        public DefinitionToken Name { get; }
        public List<DefinitionToken> Parameters { get; }

        public FunctionBlock(IBlock parrent, BlockType type, TrackingToken tokenStart, DefinitionToken name) : base(parrent, type, tokenStart)
        {
            Name = name;
            Parameters = new List<DefinitionToken>();
            parrent.AddToken(name);
        }

        public override void AddToken(AnalysisToken token)
        {
            base.AddToken(token);
            if (token.Type == RadAsmTokenType.FunctionParameter)
                Parameters.Add(token as DefinitionToken);
        }

        public override bool InRange(int point) =>
            Name.Span.End <= point && actualEnd >= point;

        public override bool InRange(Span span) =>
            Name.Span.End <= span.Start && actualEnd >= span.End;
    }
}
