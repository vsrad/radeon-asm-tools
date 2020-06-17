using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Parser.RadAsm
{
    internal class AsmParser : Parser
    {
        public override List<IBlock> Parse(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            var blocks = new List<IBlock>();
            var referenceCandidate = new Dictionary<string, List<KeyValuePair<IBlock, TrackingToken>>>();
            var definitionTokens = new List<AnalysisToken>();

            var tokens = trackingTokens
                .Where(t => t.Type != RadAsmLexer.WHITESPACE && t.Type != RadAsmLexer.LINE_COMMENT)
                .ToArray();
            var currentBlock = Block.Empty();
            var parserState = ParserState.SearchInScope;
            var parameters = new HashSet<string>();
            var searchInFunction = false;

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

                            definitionTokens.Add(analysisToken);
                            i += 1;
                        }
                    }
                    else if (token.Type == RadAsmLexer.ENDM && currentBlock.Type == BlockType.Function)
                    {
                        parameters.Clear();
                        currentBlock.SetEnd(version, token.GetEnd(version), token);
                        currentBlock = SetBlockReady(currentBlock, blocks);

                        parserState = ParserState.SearchInScope;
                        searchInFunction = false;
                    }
                    else if (token.Type == RadAsmLexer.EOL)
                    {
                        if (tokens.Length - i > 3
                            && tokens[i + 1].Type == RadAsmLexer.IDENTIFIER
                            && tokens[i + 2].Type == RadAsmLexer.COLON
                            && tokens[i + 3].Type == RadAsmLexer.EOL)
                        {
                            var analysisToken = new AnalysisToken(RadAsmTokenType.Label, tokens[i + 1]);
                            currentBlock.Tokens.Add(analysisToken);
                            definitionTokens.Add(analysisToken);
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
                        parserState = ParserState.SearchConditions;
                    }
                    else if ((token.Type == RadAsmLexer.ELSEIF || token.Type == RadAsmLexer.ELSE) && currentBlock.Type == BlockType.Condition)
                    {
                        currentBlock.SetEnd(version, tokens[i - 1].Start.GetPosition(version), token);
                        currentBlock = SetBlockReady(currentBlock, blocks);

                        currentBlock = new Block(currentBlock, BlockType.Condition, token);
                        parserState = ParserState.SearchConditions;
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
                        parserState = ParserState.SearchConditions;
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
                            if (parameters.Contains(token.GetText(version)))
                            {
                                currentBlock.AddToken(RadAsmTokenType.FunctionParameterReference, token);
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
                            currentBlock.AddToken(RadAsmTokenType.Include, tokens[i + 1]);
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
                        currentBlock.AddToken(RadAsmTokenType.FunctionParameter, token);
                        parameters.Add("\\" + token.GetText(version));
                    }
                }
                else if (parserState == ParserState.SearchConditions)
                {
                    if (token.Type == RadAsmLexer.EOL)
                    {
                        currentBlock.SetScopeStart(tokens[i - 1].GetEnd(version));
                        parserState = ParserState.SearchInScope;
                    }
                }
            }

            foreach (var definitionToken in definitionTokens)
            {
                cancellation.ThrowIfCancellationRequested();

                var tokenText = definitionToken.TrackingToken.GetText(version);
                if (referenceCandidate.TryGetValue(tokenText, out var referenceTokens))
                {
                    foreach (var referenceToken in referenceTokens)
                    {
                        var type = definitionToken.Type == RadAsmTokenType.FunctionName
                            ? RadAsmTokenType.FunctionReference
                            : definitionToken.Type == RadAsmTokenType.Label
                                ? RadAsmTokenType.LabelReference : RadAsmTokenType.Unknown;

                        referenceToken.Key.AddToken(type, referenceToken.Value);
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
