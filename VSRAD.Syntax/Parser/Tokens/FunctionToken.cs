using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    class FunctionToken : BaseToken, IDescriptionToken
    {
        public string Description { get; } = null;

        public FunctionToken(SnapshotSpan symbolSpan, string description = "") : base(symbolSpan, TokenType.Function)
        {
            Description = description;
        }
    }
}
