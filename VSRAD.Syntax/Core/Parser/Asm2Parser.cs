using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Options.Instructions;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.Parser
{
    internal class Asm2Parser : AbstractInstructionParser
    {
        public Asm2Parser(IDocumentFactory documentFactory, IInstructionListManager instructionManager) 
            : base(documentFactory, instructionManager, Helpers.AsmType.RadAsm2) { }

        public override async Task<List<IBlock>> RunAsync(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            var blocks = new List<IBlock>();
            var referenceCandidate = new Dictionary<string, List<KeyValuePair<IBlock, TrackingToken>>>();
            var definitionTokens = new List<KeyValuePair<AnalysisToken, ITextSnapshot>>();

            var tokens = trackingTokens
                .Where(t => t.Type != RadAsm2Lexer.WHITESPACE && t.Type != RadAsm2Lexer.LINE_COMMENT)
                .ToArray();

            var currentBlock = Block.Empty();
            var parserState = ParserState.SearchInScope;
            var parameters = new Dictionary<string, AnalysisToken>();
            var parenthCnt = 0;
            var searchInFunction = false;
            var searchInCondition = false;

            blocks.Add(currentBlock);
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];

                if (parserState == ParserState.SearchInScope)
                {
                    if (token.Type == RadAsm2Lexer.BLOCK_COMMENT)
                    {
                        var commentBlock = new Block(currentBlock, BlockType.Comment, token);
                        commentBlock.SetScope(version, token.GetSpan(version));
                        currentBlock = SetBlockReady(commentBlock, blocks);
                    }
                    else if (token.Type == RadAsm2Lexer.EOL)
                    {
                        if (searchInCondition)
                        {
                            currentBlock.SetScopeStart(tokens[i - 1].GetEnd(version));
                            searchInCondition = false;
                        }

                        if (tokens.Length - i > 3
                            && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER
                            && tokens[i + 2].Type == RadAsm2Lexer.COLON
                            && tokens[i + 3].Type == RadAsm2Lexer.EOL)
                        {
                            var analysisToken = new AnalysisToken(RadAsmTokenType.Label, tokens[i + 1]);
                            currentBlock.Tokens.Add(analysisToken);
                            definitionTokens.Add(new KeyValuePair<AnalysisToken, ITextSnapshot>(analysisToken, version));
                            i += 2;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.FUNCTION)
                    {
                        if (tokens.Length - i > 2 && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER)
                        {
                            parameters.Clear();
                            if (tokens[i + 2].Type == RadAsm2Lexer.EOL)
                            {
                                var analysisToken = new AnalysisToken(RadAsmTokenType.FunctionName, tokens[i + 1]);
                                currentBlock = new FunctionBlock(currentBlock, BlockType.Function, token, analysisToken);
                                currentBlock.SetScopeStart(tokens[i + 1].GetEnd(version));

                                definitionTokens.Add(new KeyValuePair<AnalysisToken, ITextSnapshot>(analysisToken, version));
                                searchInFunction = true;
                                i += 1;
                            }
                            else if (tokens[i + 2].Type == RadAsm2Lexer.LPAREN)
                            {
                                var analysisToken = new AnalysisToken(RadAsmTokenType.FunctionName, tokens[i + 1]);
                                currentBlock = new FunctionBlock(currentBlock, BlockType.Function, token, analysisToken);
                                parserState = ParserState.SearchArguments;

                                definitionTokens.Add(new KeyValuePair<AnalysisToken, ITextSnapshot>(analysisToken, version));
                                parenthCnt = 1;
                                i += 2;
                            }
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.IF)
                    {
                        currentBlock = new Block(currentBlock, BlockType.Condition, token);
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm2Lexer.ELSIF || token.Type == RadAsm2Lexer.ELSE)
                    {
                        if (tokens.Length > 2)
                        {
                            currentBlock.SetEnd(version, tokens[i - 1].Start.GetPosition(version), token);
                            currentBlock = SetBlockReady(currentBlock, blocks);

                            currentBlock = new Block(currentBlock, BlockType.Condition, token);
                            searchInCondition = true;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.FOR || token.Type == RadAsm2Lexer.WHILE)
                    {
                        currentBlock = new Block(currentBlock, BlockType.Loop, token);
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm2Lexer.END)
                    {
                        if (currentBlock.Type == BlockType.Function)
                        {
                            searchInFunction = false;

                            currentBlock.SetEnd(version, token.GetEnd(version), token);
                            currentBlock = SetBlockReady(currentBlock, blocks);
                        }
                        else if (currentBlock.Type == BlockType.Condition || currentBlock.Type == BlockType.Loop)
                        {
                            currentBlock.SetEnd(version, token.GetEnd(version), token);
                            currentBlock = SetBlockReady(currentBlock, blocks);
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.REPEAT)
                    {
                        currentBlock = new Block(currentBlock, BlockType.Repeat, token);
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm2Lexer.UNTIL)
                    {
                        if (currentBlock.Type == BlockType.Repeat)
                        {
                            currentBlock.SetEnd(version, token.GetEnd(version), token);
                            currentBlock = SetBlockReady(currentBlock, blocks);
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.VAR)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER)
                        {
                            if (tokens.Length - i > 3 && tokens[i + 2].Type == RadAsm2Lexer.EQ && tokens[i + 3].Type == RadAsm2Lexer.CONSTANT)
                            {
                                currentBlock.Tokens.Add(new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1], tokens[i + 3]));
                            }
                            else
                            {
                                currentBlock.Tokens.Add(new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1]));
                            }
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.IDENTIFIER)
                    {
                        if (searchInFunction)
                        {
                            if (parameters.TryGetValue(token.GetText(version), out var parameterToken))
                            {
                                currentBlock.Tokens.Add(new ReferenceToken(RadAsmTokenType.FunctionParameterReference, token, parameterToken));
                                continue;
                            }
                        }

                        var tokenText = token.GetText(version);
                        if (Instructions.Contains(tokenText))
                            currentBlock.AddToken(RadAsmTokenType.Instruction, token);
                        else
                        {
                            if (referenceCandidate.TryGetValue(tokenText, out var referenceTokens))
                                referenceTokens.Add(new KeyValuePair<IBlock, TrackingToken>(currentBlock, token));
                            else
                                referenceCandidate[tokenText] = new List<KeyValuePair<IBlock, TrackingToken>>() { new KeyValuePair<IBlock, TrackingToken>(currentBlock, token) };
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.PP_INCLUDE)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.STRING_LITERAL)
                        {
                            await AddExternalDefinitionsAsync(definitionTokens, tokens[i + 1], version);
                        }
                    }
                }
                else if (parserState == ParserState.SearchArguments)
                {
                    if (token.Type == RadAsm2Lexer.LPAREN)
                    {
                        parenthCnt++;
                    }
                    else if (token.Type == RadAsm2Lexer.RPAREN)
                    {
                        if (--parenthCnt == 0)
                        {
                            currentBlock.SetScopeStart(tokens[i].GetEnd(version));

                            parserState = ParserState.SearchInScope;
                            searchInFunction = true;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.IDENTIFIER)
                    {
                        var analysisToken = new AnalysisToken(RadAsmTokenType.FunctionParameter, token);
                        currentBlock.Tokens.Add(analysisToken);
                        parameters[token.GetText(version)] = analysisToken;
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
