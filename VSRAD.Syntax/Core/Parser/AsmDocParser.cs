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

        public override Task<List<IBlock>> RunAsync(IDocument document, ITextSnapshot version, IEnumerable<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            IBlock rootBlock = new Block(version);
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
                        rootBlock.AddToken(new DefinitionToken(RadAsmTokenType.GlobalVariable, tokens[i + 1], version));
                        i += 1;
                    }
                }
                else if (tokens.Length - i > 1 && token.Type == RadAsmDocLexer.EOL && tokens[i + 1].Type == RadAsmDocLexer.IDENTIFIER)
                {
                    rootBlock.AddToken(new AnalysisToken(RadAsmTokenType.Instruction, tokens[i + 1], version));
                }
            }

            return Task.FromResult(blocks);
        }
    }
}
