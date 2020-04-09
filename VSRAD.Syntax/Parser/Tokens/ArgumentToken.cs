using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    class ArgumentToken : BaseToken
    {
        public ArgumentToken(SnapshotSpan symbolSpan) : base(symbolSpan, TokenType.Argument)
        {
        }
    }
}
