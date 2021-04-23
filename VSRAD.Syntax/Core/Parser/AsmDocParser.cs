using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.Parser
{
    internal class AsmDocParser : IParser
    {
        public Task<IParserResult> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            var definitions = new Dictionary<string, DefinitionToken>();
            IBlock rootBlock = new Block();
            var blocks = new List<IBlock>() { rootBlock };
            var tokens = trackingTokens
                .Where(t => t.Type != RadAsmDocLexer.WHITESPACE && t.Type != RadAsmDocLexer.BLOCK_COMMENT)
                .AsParallel()
                .AsOrdered()
                .WithCancellation(cancellation)
                .ToArray();

            InstructionToken currentInstruction = null;

            for (int i = 0; i < tokens.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var token = tokens[i];

                if (token.Type == RadAsmDocLexer.LET)
                {
                    if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmDocLexer.IDENTIFIER)
                    {
                        var definition = new VariableToken(RadAsmTokenType.GlobalVariable, tokens[i + 1], version);
                        definitions.Add(definition.GetText(), definition);
                        i += 1;
                    }
                }
                else if (tokens.Length - i > 1 && token.Type == RadAsmDocLexer.EOL && tokens[i + 1].Type == RadAsmDocLexer.IDENTIFIER)
                {
                    currentInstruction = new InstructionToken(RadAsmTokenType.Instruction, tokens[i + 1], version);
                    rootBlock.AddToken(currentInstruction);
                    i += 1;
                }
                else if (token.Type == RadAsmDocLexer.IDENTIFIER)
                {
                    var text = token.GetText(version);

                    if (definitions.TryGetValue(text, out var definition))
                    {
                        var reference = new ReferenceToken(RadAsmTokenType.GlobalVariableReference, token, version, definition);
                        rootBlock.AddToken(reference);
                        currentInstruction?.Parameters.Add(reference);
                    }
                    else if (currentInstruction != null)
                    {
                        var parameter = new AnalysisToken(RadAsmTokenType.GlobalVariableReference, token, version);
                        currentInstruction.Parameters.Add(parameter);
                    }
                }
                else if (token.Type == RadAsmDocLexer.LCURVEBRACKET || token.Type == RadAsmDocLexer.EOL)
                {
                    currentInstruction = null;
                }
            }

            var result = new ParserResult(blocks, new List<IErrorToken>());
            return Task.FromResult((IParserResult)result);
        }
    }
}
