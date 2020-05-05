using Microsoft.VisualStudio.Text;
using System.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    public class FunctionToken : BaseToken, IDescriptionToken
    {
        public string Description { get; } = null;

        public FunctionToken(SnapshotSpan symbolSpan, string description) : base(symbolSpan, TokenType.Function)
        {
            Description = description;
        }

        public string GetFullName()
        {
            var parserManager = Line.Snapshot.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());
            var parser = parserManager.ActualParser;
            if (parser == null)
                return TokenName;

            var fb = parser.GetFunctionByToken(this);
            if (fb == null)
                return TokenName;

            var sb = new StringBuilder();
            sb.Append(TokenName).Append(" ");
            foreach (var arg in fb.GetArgumentTokens())
            {
                sb.Append(arg.TokenName).Append(", ");
            }
            return sb.ToString().TrimEnd(' ', ',');
        }
    }
}
