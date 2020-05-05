using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    internal class GlobalVariableToken : VariableToken
    {
        public GlobalVariableToken(SnapshotSpan symbolSpan, string description) : base(symbolSpan, description, TokenType.GlobalVariable)
        {
        }
    }

    internal class LocalVariableToken : VariableToken
    {
        public LocalVariableToken(SnapshotSpan symbolSpan, string description) : base(symbolSpan, description, TokenType.LocalVariable)
        {
        }
    }

    internal class VariableToken : BaseToken, IDescriptionToken
    {
        public string Description { get; } = null;

        public VariableToken(SnapshotSpan symbolSpan, string description, TokenType tokenType = TokenType.GlobalVariable) : base(symbolSpan, tokenType)
        {
            Description = description;
        }
    }
}
