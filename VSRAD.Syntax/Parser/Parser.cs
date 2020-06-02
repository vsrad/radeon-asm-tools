using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Parser
{
    public interface IParser
    {
        List<IBlock> Run(int lexerVersion, IEnumerable<TrackingToken> tokens, ITextSnapshot version, CancellationToken cancellation);
        void UpdateInstructionSet(IReadOnlyList<string> instructions);
    }

    internal abstract class Parser : IParser
    {
        protected int _currentVersion;
        protected ITextSnapshot _snapshot;
        protected HashSet<string> _instructions;

        public Parser()
        {
            _currentVersion = -1;
            _instructions = new HashSet<string>();
        }

        public List<IBlock> Run(int lexerVersion, IEnumerable<TrackingToken> tokens, ITextSnapshot snapshot, CancellationToken cancellation)
        {
            if (lexerVersion == _currentVersion)
                return null;

            _currentVersion = lexerVersion;
            _snapshot = snapshot;
            return Parse(tokens, _snapshot, cancellation);
        }

        public void UpdateInstructionSet(IReadOnlyList<string> instructions) =>
            _instructions = instructions.ToHashSet();

        public static IBlock SetBlockReady(IBlock block, List<IBlock> list)
        {
            if (block.Scope != TrackingBlock.Empty)
                list.Add(block);

            if (block.Parrent != null)
                block.Parrent.AddChildren(block);

            return block.Parrent ?? block;
        }

        public abstract List<IBlock> Parse(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation);
    }

    internal class AsmParser : Parser
    {
        public override List<IBlock> Parse(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            var blocks = new List<IBlock>();
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
                            currentBlock = new FunctionBlock(currentBlock, BlockType.Function, token, new AnalysisToken(RadAsmTokenType.FunctionName, tokens[i + 1]));
                            parserState = ParserState.SearchArguments;
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
                            currentBlock.AddToken(RadAsmTokenType.Label, tokens[i + 1]);
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

                        if (_instructions.Contains(token.GetText(version)))
                            currentBlock.AddToken(RadAsmTokenType.Instruction, token);
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

    internal class Asm2Parser : Parser
    {
        public override List<IBlock> Parse(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            var blocks = new List<IBlock>();
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
                            currentBlock.AddToken(RadAsmTokenType.Label, tokens[i + 1]);
                            i += 2;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.FUNCTION)
                    {
                        if (tokens.Length - i > 2 && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER)
                        {
                            if (tokens[i + 2].Type == RadAsm2Lexer.EOL)
                            {
                                currentBlock = new FunctionBlock(currentBlock, BlockType.Function, token, new AnalysisToken(RadAsmTokenType.FunctionName, tokens[i + 1]));
                                currentBlock.SetScopeStart(tokens[i + 1].GetEnd(version));
                                searchInFunction = true;
                                i += 1;
                            }
                            else if (tokens[i + 2].Type == RadAsm2Lexer.LPAREN)
                            {
                                currentBlock = new FunctionBlock(currentBlock, BlockType.Function, token, new AnalysisToken(RadAsmTokenType.FunctionName, tokens[i + 1]));
                                parserState = ParserState.SearchArguments;
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

                        if (_instructions.Contains(token.GetText(version)))
                            currentBlock.AddToken(RadAsmTokenType.Instruction, token);
                    }
                    else if (token.Type == RadAsm2Lexer.PP_INCLUDE)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.STRING_LITERAL)
                        {
                            currentBlock.AddToken(RadAsmTokenType.Include, tokens[i + 1]);
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
