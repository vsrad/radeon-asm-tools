using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Parser.RadAsm2
{
    internal class Asm2Parser : Parser
    {
        public Asm2Parser(DocumentInfo documentInfo, DocumentAnalysisProvoder documentAnalysisProvoder) 
            : base(documentInfo, documentAnalysisProvoder) { }

        public override List<IBlock> Parse(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation)
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
            var parameters = new HashSet<string>();
            var parenthCnt = 0;
            var searchInFunction = false;

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
                        parserState = ParserState.SearchConditions;
                    }
                    else if (token.Type == RadAsm2Lexer.ELSIF || token.Type == RadAsm2Lexer.ELSE)
                    {
                        if (tokens.Length > 2)
                        {
                            currentBlock.SetEnd(version, tokens[i - 1].Start.GetPosition(version), token);
                            currentBlock = SetBlockReady(currentBlock, blocks);

                            currentBlock = new Block(currentBlock, BlockType.Condition, token);
                            parserState = ParserState.SearchConditions;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.FOR || token.Type == RadAsm2Lexer.WHILE)
                    {
                        currentBlock = new Block(currentBlock, BlockType.Loop, token);
                        parserState = ParserState.SearchConditions;
                    }
                    else if (token.Type == RadAsm2Lexer.END)
                    {
                        if (currentBlock.Type == BlockType.Function)
                        {
                            searchInFunction = false;
                            parameters.Clear();

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
                        parserState = ParserState.SearchConditions;
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
                            if (parameters.Contains(token.GetText(version)))
                            {
                                currentBlock.AddToken(RadAsmTokenType.FunctionParameterReference, token);
                                continue;
                            }
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
                    else if (token.Type == RadAsm2Lexer.PP_INCLUDE)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.STRING_LITERAL)
                        {
                            AddExternalDefinitions(definitionTokens, tokens[i + 1], version);
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
                        currentBlock.AddToken(RadAsmTokenType.FunctionParameter, token);
                        parameters.Add(token.GetText(version));
                    }
                }
                else if (parserState == ParserState.SearchConditions)
                {
                    if (token.Type == RadAsm2Lexer.EOL)
                    {
                        currentBlock.SetScopeStart(tokens[i - 1].GetEnd(version));
                        parserState = ParserState.SearchInScope;
                    }
                }
            }

            foreach (var definitionTokenPair in definitionTokens)
            {
                cancellation.ThrowIfCancellationRequested();

                var definitionToken = definitionTokenPair.Key;
                var tokenText = definitionToken.TrackingToken.GetText(definitionTokenPair.Value);
                if (referenceCandidate.TryGetValue(tokenText, out var referenceTokens))
                {
                    foreach (var referenceToken in referenceTokens)
                    {
                        var type = definitionToken.Type == RadAsmTokenType.FunctionName
                            ? RadAsmTokenType.FunctionReference
                            : definitionToken.Type == RadAsmTokenType.Label
                                ? RadAsmTokenType.LabelReference : RadAsmTokenType.Unknown;

                        referenceToken.Key.Tokens.Add(new ReferenceToken(type, referenceToken.Value, definitionToken));
                    }
                }
            }

            return blocks;
        }

        private enum ParserState
        {
            SearchInScope = 1,
            SearchArguments = 2,
            SearchArgAttribute = 3,
            SearchConditions = 4,
        }
    }
}
