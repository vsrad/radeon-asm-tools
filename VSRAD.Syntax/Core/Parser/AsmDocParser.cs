using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.Parser
{
    internal class AsmDocParser : AbstractParser
    {
        public AsmDocParser(IDocumentFactory documentFactory) 
            : base(documentFactory) { }

        public override Task<List<IBlock>> RunAsync(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            var rootBlock = Block.Empty();
            var blocks = new List<IBlock>() { rootBlock };
            var tokens = trackingTokens
                .Where(t => t.Type != RadAsmDocLexer.WHITESPACE)
                .ToArray();

            for (int i = 0; i < tokens.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var token = tokens[i];

                if (token.Type == RadAsmDocLexer.LET)
                {
                    if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmDocLexer.IDENTIFIER)
                    {
                        rootBlock.Tokens.Add(new VariableToken(RadAsmTokenType.GlobalVariable, tokens[i + 1]));
                        i += 1;
                    }
                }
                else if (tokens.Length - i > 1 && token.Type == RadAsmDocLexer.EOL && tokens[i + 1].Type == RadAsmDocLexer.IDENTIFIER)
                {
                    rootBlock.AddToken(RadAsmTokenType.Instruction, tokens[i + 1]);
                }
            }

            return Task.FromResult(blocks);
        }
    }
}
