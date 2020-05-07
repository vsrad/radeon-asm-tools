using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Text;
using VSRAD.Syntax.Parser.Blocks;

namespace VSRAD.Syntax.Parser.Tokens
{
    public class FunctionToken : BaseToken, IDescriptionToken
    {
        public string Description { get; } = null;

        public FunctionToken(SnapshotSpan symbolSpan, string description) : base(symbolSpan, TokenType.Function)
        {
            Description = description;
        }

        public FunctionBlock GetFunctionBlock()
        {
            var parserManager = Line.Snapshot.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());
            var parser = parserManager.ActualParser;
            if (parser == null)
                return null;

            return parser.GetFunctionByToken(this);
        }
    }
}
