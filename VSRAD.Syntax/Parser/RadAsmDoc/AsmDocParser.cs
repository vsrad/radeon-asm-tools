using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Parser.RadAsmDoc
{
    internal class AsmDocParser : Parser
    {
        public override List<IBlock> Parse(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            var rootBlock = Block.Empty();
            var blocks = new List<IBlock>() { rootBlock };
            var definitions = new HashSet<string>();
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
                        definitions.Add(tokens[i + 1].GetText(version));
                        i += 1;
                    }
                }
                else if (token.Type == RadAsmDocLexer.IDENTIFIER)
                {
                    if (!definitions.Contains(token.GetText(version)))
                        rootBlock.AddToken(RadAsmTokenType.Instruction, token);
                }
            }

            return blocks;
        }
    }
}
