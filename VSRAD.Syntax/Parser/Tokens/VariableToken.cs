using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    internal class VariableToken : BaseToken, IDescriptionToken
    {
        public string Description { get; } = null;

        public VariableToken(SnapshotSpan symbolSpan, string description = "") : base(symbolSpan, TokenType.Variable)
        {
            Description = description;
        }
    }
}
