﻿using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Options.Instructions;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.Parser
{
    internal class Asm2Parser : AbstractInstructionParser
    {
        public Asm2Parser(IDocumentFactory documentFactory, IInstructionListManager instructionManager) 
            : base(documentFactory, instructionManager, Helpers.AsmType.RadAsm2) { }

        public override async Task<List<IBlock>> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            var tokens = trackingTokens
                .Where(t => t.Type != RadAsm2Lexer.WHITESPACE && t.Type != RadAsm2Lexer.LINE_COMMENT)
                .AsParallel()
                .AsOrdered()
                .WithCancellation(cancellation)
                .ToArray();

            var referenceCandidates = new LinkedList<(string, TrackingToken, IBlock)>();
            _definitionContainer.Clear();
            var blocks = new List<IBlock>();
            IBlock currentBlock = new Block(version);
            var parserState = ParserState.SearchInScope;
            var parenthCnt = 0;
            var searchInCondition = false;

            blocks.Add(currentBlock);
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];

                if (parserState == ParserState.SearchInScope)
                {
                    if (token.Type == RadAsm2Lexer.BLOCK_COMMENT)
                    {
                        var commentBlock = new Block(currentBlock, BlockType.Comment, token, token);
                        currentBlock = SetBlockReady(commentBlock, blocks);
                    }
                    else if (token.Type == RadAsm2Lexer.EOL)
                    {
                        if (searchInCondition)
                        {
                            currentBlock.SetStart(tokens[i - 1].GetEnd(version));
                            searchInCondition = false;
                        }

                        if (tokens.Length - i > 3
                            && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER
                            && tokens[i + 2].Type == RadAsm2Lexer.COLON
                            && tokens[i + 3].Type == RadAsm2Lexer.EOL)
                        {
                            var labelDefinition = new DefinitionToken(RadAsmTokenType.Label, tokens[i + 1], version);
                            _definitionContainer.Add(currentBlock, labelDefinition);
                            currentBlock.AddToken(labelDefinition);
                            i += 2;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.FUNCTION)
                    {
                        if (tokens.Length - i > 2 && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER)
                        {
                            if (tokens[i + 2].Type == RadAsm2Lexer.EOL)
                            {
                                var funcDefinition = new DefinitionToken(RadAsmTokenType.FunctionName, tokens[i + 1], version);
                                _definitionContainer.Add(currentBlock, funcDefinition);
                                currentBlock = new FunctionBlock(currentBlock, BlockType.Function, token, funcDefinition);
                                currentBlock.SetStart(tokens[i + 1].GetEnd(version));
                                i += 1;
                            }
                            else if (tokens[i + 2].Type == RadAsm2Lexer.LPAREN)
                            {
                                var funcDefinition = new DefinitionToken(RadAsmTokenType.FunctionName, tokens[i + 1], version);
                                _definitionContainer.Add(currentBlock, funcDefinition);
                                currentBlock = new FunctionBlock(currentBlock, BlockType.Function, token, funcDefinition);
                                parserState = ParserState.SearchArguments;

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
                            currentBlock.SetEnd(tokens[i - 1].Start.GetPosition(version), token);
                            _definitionContainer.ClearScope(currentBlock);
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
                        if (currentBlock.Type == BlockType.Function || currentBlock.Type == BlockType.Condition || currentBlock.Type == BlockType.Loop)
                        {
                            currentBlock.SetEnd(token.GetEnd(version), token);
                            _definitionContainer.ClearScope(currentBlock);
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
                            currentBlock.SetEnd(token.GetEnd(version), token);
                            _definitionContainer.ClearScope(currentBlock);
                            currentBlock = SetBlockReady(currentBlock, blocks);
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.VAR)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER)
                        {
                            var variableDefinition = (tokens.Length - i > 3 && tokens[i + 2].Type == RadAsm2Lexer.EQ && tokens[i + 3].Type == RadAsm2Lexer.CONSTANT)
                                ? new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1], version, tokens[i + 3])
                                : new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1], version);
                            _definitionContainer.Add(currentBlock, variableDefinition);
                            currentBlock.AddToken(variableDefinition);
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.IDENTIFIER)
                    {
                        var tokenText = token.GetText(version);
                        if (Instructions.Contains(tokenText))
                        {
                            currentBlock.AddToken(new AnalysisToken(RadAsmTokenType.Instruction, token, version));
                        }
                        else if (!TryAddReference(tokenText, token, currentBlock, version))
                        {
                            referenceCandidates.AddLast((tokenText, token, currentBlock));
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.PP_INCLUDE)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.STRING_LITERAL)
                        {
                            await AddExternalDefinitionsAsync(document.Path, tokens[i + 1], currentBlock, _definitionContainer);
                            i += 1;
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
                            currentBlock.SetStart(tokens[i].GetEnd(version));
                            parserState = ParserState.SearchInScope;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.IDENTIFIER)
                    {
                        var parameterDefinition = new DefinitionToken(RadAsmTokenType.FunctionParameter, token, version);
                        _definitionContainer.Add(currentBlock, parameterDefinition);
                        currentBlock.AddToken(parameterDefinition);
                    }
                }
            }

            foreach (var referenceCandidate in referenceCandidates)
                TryAddReference(referenceCandidate.Item1, referenceCandidate.Item2, referenceCandidate.Item3, version);

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
