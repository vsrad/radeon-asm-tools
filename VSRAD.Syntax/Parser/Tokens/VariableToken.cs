using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    internal class VariableToken : BaseToken
    {
        public VariableToken(SnapshotSpan symbolSpan) : base(symbolSpan, TokenType.Variable)
        {
        }
    }
}
