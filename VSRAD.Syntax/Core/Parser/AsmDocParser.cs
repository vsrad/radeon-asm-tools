﻿using Microsoft.VisualStudio.Text;
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
    internal class AsmDocParser : AbstractParser
    {
        public AsmDocParser() 
            : base(null) { }

        public override Task<List<IBlock>> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            var definitions = new Dictionary<string, DefinitionToken>();
            IBlock rootBlock = new Block(version);
            var blocks = new List<IBlock>() { rootBlock };
            var tokens = trackingTokens
                .Where(t => t.Type != RadAsmDocLexer.WHITESPACE && t.Type != RadAsmDocLexer.BLOCK_COMMENT)
                .AsParallel()
                .AsOrdered()
                .WithCancellation(cancellation)
                .ToArray();

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
                    rootBlock.AddToken(new AnalysisToken(RadAsmTokenType.Instruction, tokens[i + 1], version));
                }
                else if (token.Type == RadAsmDocLexer.IDENTIFIER)
                {
                    var text = token.GetText(version);
                    if (definitions.TryGetValue(text, out var definition))
                        rootBlock.AddToken(new ReferenceToken(RadAsmTokenType.GlobalVariableReference, token, version, definition));
                }
            }

            return Task.FromResult(blocks);
        }
    }
}