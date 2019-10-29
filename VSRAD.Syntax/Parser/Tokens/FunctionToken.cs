using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    class FunctionToken : BaseToken
    {
        public FunctionToken(SnapshotSpan symbolSpan) : base(symbolSpan, TokenType.Function)
        {
        }
    }
}
