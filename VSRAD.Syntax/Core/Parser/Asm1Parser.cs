using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.RadAsm
{
    [Export(typeof(Asm1Parser))]
    internal class Asm1Parser : AbstractParser
    {
        public Asm1Parser(IDocumentFactory documentFactory)
            : base(documentFactory) { }

        public override async Task<List<IBlock>> RunAsync(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            var blocks = new List<IBlock>();
            var referenceCandidate = new Dictionary<string, List<KeyValuePair<IBlock, TrackingToken>>>();
            var definitionTokens = new List<KeyValuePair<AnalysisToken, ITextSnapshot>>();

            var tokens = trackingTokens
                .Where(t => t.Type != RadAsmLexer.WHITESPACE && t.Type != RadAsmLexer.LINE_COMMENT)
                .ToArray();
            var currentBlock = Block.Empty();
            var parserState = ParserState.SearchInScope;
            var parameters = new Dictionary<string, AnalysisToken>();
            var searchInFunction = false;
            var searchInCondition = false;

            blocks.Add(currentBlock);
            for (var i = 0; i < tokens.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var token = tokens[i];

                if (parserState == ParserState.SearchInScope)
                {
                    if (token.Type == RadAsmLexer.BLOCK_COMMENT)
                    {
                        var commentBlock = new Block(currentBlock, BlockType.Comment, token);
                        commentBlock.SetScope(version, token.GetSpan(version));
                        currentBlock = SetBlockReady(commentBlock, blocks);
                    }
                    else if (token.Type == RadAsmLexer.MACRO)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmLexer.IDENTIFIER)
                        {
                            var analysisToken = new AnalysisToken(RadAsmTokenType.FunctionName, tokens[i + 1]);
                            currentBlock = new FunctionBlock(currentBlock, BlockType.Function, token, analysisToken);
                            parserState = ParserState.SearchArguments;
                            parameters.Clear();

                            definitionTokens.Add(new KeyValuePair<AnalysisToken, ITextSnapshot>(analysisToken, version));
                            i += 1;
                        }
                    }
                    else if (token.Type == RadAsmLexer.ENDM && currentBlock.Type == BlockType.Function)
                    {
                        currentBlock.SetEnd(version, token.GetEnd(version), token);
                        currentBlock = SetBlockReady(currentBlock, blocks);

                        parserState = ParserState.SearchInScope;
                        searchInFunction = false;
                    }
                    else if (token.Type == RadAsmLexer.EOL)
                    {
                        if (searchInCondition)
                        {
                            currentBlock.SetScopeStart(tokens[i - 1].GetEnd(version));
                            searchInCondition = false;
                        }

                        if (tokens.Length - i > 3
                            && tokens[i + 1].Type == RadAsmLexer.IDENTIFIER
                            && tokens[i + 2].Type == RadAsmLexer.COLON
                            && tokens[i + 3].Type == RadAsmLexer.EOL)
                        {
                            var analysisToken = new AnalysisToken(RadAsmTokenType.Label, tokens[i + 1]);
                            currentBlock.Tokens.Add(analysisToken);
                            definitionTokens.Add(new KeyValuePair<AnalysisToken, ITextSnapshot>(analysisToken, version));
                            i += 2;
                        }
                    }
                    else if (token.Type == RadAsmLexer.IF
                        || token.Type == RadAsmLexer.IFDEF
                        || token.Type == RadAsmLexer.IFNOTDEF
                        || token.Type == RadAsmLexer.IFB
                        || token.Type == RadAsmLexer.IFC
                        || token.Type == RadAsmLexer.IFEQ
                        || token.Type == RadAsmLexer.IFEQS
                        || token.Type == RadAsmLexer.IFGE
                        || token.Type == RadAsmLexer.IFGT
                        || token.Type == RadAsmLexer.IFLE
                        || token.Type == RadAsmLexer.IFLT
                        || token.Type == RadAsmLexer.IFNB
                        || token.Type == RadAsmLexer.IFNC
                        || token.Type == RadAsmLexer.IFNE
                        || token.Type == RadAsmLexer.IFNES)
                    {
                        currentBlock = new Block(currentBlock, BlockType.Condition, token);
                        searchInCondition = true;
                    }
                    else if ((token.Type == RadAsmLexer.ELSEIF || token.Type == RadAsmLexer.ELSE) && currentBlock.Type == BlockType.Condition)
                    {
                        currentBlock.SetEnd(version, tokens[i - 1].Start.GetPosition(version), token);
                        currentBlock = SetBlockReady(currentBlock, blocks);

                        currentBlock = new Block(currentBlock, BlockType.Condition, token);
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsmLexer.ENDIF && currentBlock.Type == BlockType.Condition)
                    {
                        currentBlock.SetEnd(version, token.GetEnd(version), token);
                        currentBlock = SetBlockReady(currentBlock, blocks);
                    }
                    else if (token.Type == RadAsmLexer.REPT
                        || token.Type == RadAsmLexer.IRP
                        || token.Type == RadAsmLexer.IRPC)
                    {
                        currentBlock = new Block(currentBlock, BlockType.Repeat, token);
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsmLexer.ENDR && currentBlock.Type == BlockType.Repeat)
                    {
                        currentBlock.SetEnd(version, token.GetEnd(version), token);
                        currentBlock = SetBlockReady(currentBlock, blocks);
                    }
                    else if (token.Type == RadAsmLexer.SET)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmLexer.IDENTIFIER)
                        {
                            if (tokens.Length - i > 3 && tokens[i + 2].Type == RadAsmLexer.COMMA && tokens[i + 3].Type == RadAsmLexer.CONSTANT)
                            {
                                currentBlock.Tokens.Add(new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1], tokens[i + 3]));
                            }
                            else
                            {
                                currentBlock.Tokens.Add(new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1]));
                            }
                        }
                    }
                    else if (token.Type == RadAsmLexer.IDENTIFIER)
                    {
                        if (searchInFunction)
                        {
                            if (parameters.TryGetValue(token.GetText(version), out var parameterToken))
                            {
                                currentBlock.Tokens.Add(new ReferenceToken(RadAsmTokenType.FunctionParameterReference, token, parameterToken));
                                continue;
                            }
                        }

                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmLexer.EQ)
                        {
                            currentBlock.Tokens.Add(new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, token));
                            continue;
                        }

                        var tokenText = token.GetText(version);
                        if (_instructions.Contains(tokenText))
                            currentBlock.AddToken(RadAsmTokenType.Instruction, token);
                        else
                        {
                            if (referenceCandidate.TryGetValue(tokenText, out var referenceTokens))
                                referenceTokens.Add(new KeyValuePair<IBlock, TrackingToken>(currentBlock, token));
                            else
                                referenceCandidate[tokenText] = new List<KeyValuePair<IBlock, TrackingToken>>() { new KeyValuePair<IBlock, TrackingToken>(currentBlock, token) };
                        }
                    }
                    else if (token.Type == RadAsmLexer.INCLUDE
                        || token.Type == RadAsmLexer.PP_INCLUDE)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmLexer.STRING_LITERAL)
                        {
                            await AddExternalDefinitionsAsync(definitionTokens, tokens[i + 1], version);
                            i += 1;
                        }
                    }
                }
                else if (parserState == ParserState.SearchArguments)
                {
                    if (token.Type == RadAsmLexer.EOL)
                    {
                        currentBlock.SetScopeStart(tokens[i - 1].GetEnd(version));
                        parserState = ParserState.SearchInScope;
                        searchInFunction = true;
                        continue;
                    }

                    if (token.Type == RadAsmLexer.IDENTIFIER)
                    {
                        var analysisToken = new AnalysisToken(RadAsmTokenType.FunctionParameter, token);
                        currentBlock.Tokens.Add(analysisToken);
                        parameters["\\" + token.GetText(version)] = analysisToken;
                    }
                }
            }

            ParseReferenceCandidate(definitionTokens, referenceCandidate, cancellation);
            return blocks;
        }

        private enum ParserState
        {
            SearchInScope = 1,
            SearchArguments = 2,
            SearchArgAttribute = 3,
        }
    }
}
