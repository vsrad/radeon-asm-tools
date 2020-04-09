using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    public class LabelToken : BaseToken
    {
        public LabelToken(SnapshotSpan symbolSpan) : base(symbolSpan, TokenType.Label)
        {
        }
    }
}
