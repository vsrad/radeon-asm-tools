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
        public static IParser Instance = new AsmDocParser();

        public Task<ParserResult> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            var definitions = new Dictionary<string, DefinitionToken>();

            var blocks = new List<IBlock>();
            var rootBlock = new Block(version);
            blocks.Add(rootBlock);

            var tokens = trackingTokens.ToArray();

            var currentBlock = rootBlock;
            for (int i = 0; i < tokens.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var token = tokens[i];

                if (token.Type == RadAsmDocLexer.TARGETS)
                {
                    if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmDocLexer.IDENTIFIER_LIST)
                    {
                        var definition = new DefinitionToken(RadAsmTokenType.GlobalVariable, tokens[i], version);
                        var targetList = tokens[i + 1].GetText(version).Trim('{', '}', ' ').Split(',').Select(t => t.Trim()).ToArray();
                        var targetListToken = new DocTargetListToken(RadAsmTokenType.Keyword, definition, targetList, version);
                        currentBlock.AddToken(targetListToken);
                        i += 1;
                    }
                }
                else if (token.Type == RadAsmDocLexer.LET)
                {
                    if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmDocLexer.IDENTIFIER)
                    {
                        var definition = new DefinitionToken(RadAsmTokenType.GlobalVariable, tokens[i + 1], version);
                        definitions.Add(definition.GetText(), definition);
                        i += 1;
                    }
                }
                else if (token.Type == RadAsmDocLexer.IDENTIFIER)
                {
                    if (i < 1 || tokens[i - 1].Type == RadAsmDocLexer.EOL)
                    {
                        if (i < 2 || tokens[i - 2].Type == RadAsmDocLexer.BLOCK_COMMENT)
                        {
                            if (currentBlock != rootBlock && i >= 3 && tokens[i - 3].Type == RadAsmDocLexer.EOL)
                                currentBlock.SetEnd(tokens[i - 3].GetStart(version), tokens[i - 3]);

                            var docComment = new AnalysisToken(RadAsmTokenType.Comment, tokens[i - 2], version);
                            currentBlock = blocks.AppendBlock(new InstructionDocBlock(rootBlock, docComment));
                            currentBlock.SetStart(tokens[i - 2].GetStart(version));
                        }
                        currentBlock.AddToken(new AnalysisToken(RadAsmTokenType.Instruction, token, version));
                    }
                    else
                    {
                        var text = token.GetText(version);
                        if (definitions.TryGetValue(text, out var definition))
                            currentBlock.AddToken(new ReferenceToken(RadAsmTokenType.GlobalVariableReference, token, version, definition));
                    }
                }
            }

            var result = new ParserResult(blocks, new List<IErrorToken>());

            return Task.FromResult(result);
        }
    }
}
