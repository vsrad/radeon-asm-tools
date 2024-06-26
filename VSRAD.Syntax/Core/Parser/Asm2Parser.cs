﻿using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense;
using VSRAD.Syntax.Options.Instructions;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.Parser
{
    internal sealed class Asm2Parser : AbstractCodeParser
    {
        public static IParser Instance => LazyInstance.Value;

        private static readonly Lazy<IParser> LazyInstance = new Lazy<IParser>(() =>
        {
            var serviceProvider = ServiceProvider.GlobalProvider;
            var documentFactory = serviceProvider.GetMefService<IDocumentFactory>();
            var builtinInfoProvider = serviceProvider.GetMefService<IBuiltinInfoProvider>();
            var instructionListManager = serviceProvider.GetMefService<IInstructionListManager>();

            return new Asm2Parser(documentFactory, builtinInfoProvider, instructionListManager);
        });

        private Asm2Parser(IDocumentFactory documentFactory, IBuiltinInfoProvider builtinInfoProvider, IInstructionListManager instructionListManager)
            : base(documentFactory, builtinInfoProvider, instructionListManager, AsmType.RadAsm2) { }

        public override Task<ParserResult> RunAsync(IDocument document, ITextSnapshot version,
            ITokenizerCollection<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            try
            {
                return ParseAsync(document, version, trackingTokens, cancellation);
            }
            catch (AggregateException)
            {
                // An AggregateException is thrown if the text of the document has changed (tokenizer changed too)
                // while iterating over the ITokenizerCollection. This is equivalent to canceling.
                throw new OperationCanceledException();
            }
        }

        private async Task<ParserResult> ParseAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            var tokens = new List<TrackingToken>(((ICollection<TrackingToken>)trackingTokens).Count);
            foreach (var token in trackingTokens)
            {
                cancellation.ThrowIfCancellationRequested();
                if (token.Type != RadAsm2Lexer.WHITESPACE && token.Type != RadAsm2Lexer.LINE_COMMENT)
                    tokens.Add(token);
            }

            var definitionContainer = new DefinitionContainer();
            var referenceCandidates = new List<(string text, TrackingToken trackingToken, IBlock block)>();

            var blocks = new List<IBlock>();
            var errors = new List<IErrorToken>();
            IBlock currentBlock = new Block(version);
            var parserState = ParserState.SearchInScope;
            var parenthCnt = 0;
            var searchInCondition = false;
            var preprocessBlock = false;

            blocks.Add(currentBlock);
            for (int i = 0; i < tokens.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();

                var token = tokens[i];

                if (token.Type == RadAsm2Lexer.PP_ELSE || token.Type == RadAsm2Lexer.PP_ELSIF || token.Type == RadAsm2Lexer.PP_ELIF)
                {
                    preprocessBlock = true;
                }
                else if (token.Type == RadAsm2Lexer.PP_ENDIF)
                {
                    preprocessBlock = false;
                }

                if (preprocessBlock)
                {
                    if (token.Type == RadAsm2Lexer.IDENTIFIER)
                        TryAddInstruction(token.GetText(version), token, currentBlock, version);

                    continue;
                }
                else if (parserState == ParserState.SearchInScope)
                {
                    if (token.Type == RadAsm2Lexer.BLOCK_COMMENT)
                    {
                        blocks.AppendBlock(new Block(currentBlock, BlockType.Comment, token, token));
                    }
                    else if (token.Type == RadAsm2Lexer.EOL)
                    {
                        if (searchInCondition)
                        {
                            currentBlock.SetStart(tokens[i - 1].GetEnd(version));
                            searchInCondition = false;
                        }

                        if (tokens.Count - i > 3
                            && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER
                            && tokens[i + 2].Type == RadAsm2Lexer.COLON
                            && tokens[i + 3].Type == RadAsm2Lexer.EOL)
                        {
                            var labelDefinition = new DefinitionToken(RadAsmTokenType.Label, tokens[i + 1], version);
                            definitionContainer.Add(currentBlock, labelDefinition);
                            currentBlock.AddToken(labelDefinition);
                            i += 2;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.FUNCTION || token.Type == RadAsm2Lexer.SHADER)
                    {
                        if (tokens.Count - i > 2 && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER)
                        {
                            if (tokens[i + 2].Type == RadAsm2Lexer.EOL)
                            {
                                var funcDefinition = new DefinitionToken(RadAsmTokenType.FunctionName, tokens[i + 1], version);
                                definitionContainer.Add(currentBlock, funcDefinition);
                                currentBlock = blocks.AppendBlock(new FunctionBlock(currentBlock, BlockType.Function, token, funcDefinition));
                                currentBlock.SetStart(tokens[i + 1].GetEnd(version));
                                i += 1;
                            }
                            else if (tokens[i + 2].Type == RadAsm2Lexer.LPAREN)
                            {
                                var funcDefinition = new DefinitionToken(RadAsmTokenType.FunctionName, tokens[i + 1], version);
                                definitionContainer.Add(currentBlock, funcDefinition);
                                currentBlock = blocks.AppendBlock(new FunctionBlock(currentBlock, BlockType.Function, token, funcDefinition));
                                parserState = ParserState.SearchArguments;

                                parenthCnt = 1;
                                i += 2;
                            }
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.IF)
                    {
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Condition, token));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm2Lexer.ELSIF || token.Type == RadAsm2Lexer.ELSE)
                    {
                        if (tokens.Count > 2)
                        {
                            currentBlock.SetEnd(tokens[i - 1].Start.GetPosition(version), token);
                            definitionContainer.ClearScope(currentBlock);
                            currentBlock = currentBlock.GetParent();

                            currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Condition, token));
                            searchInCondition = true;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.FOR || token.Type == RadAsm2Lexer.WHILE)
                    {
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Loop, token));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm2Lexer.END)
                    {
                        if (currentBlock.Type == BlockType.Function || currentBlock.Type == BlockType.Condition || currentBlock.Type == BlockType.Loop)
                        {
                            currentBlock.SetEnd(token.GetEnd(version), token);
                            definitionContainer.ClearScope(currentBlock);
                            currentBlock = currentBlock.GetParent();
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.REPEAT)
                    {
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Repeat, token));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm2Lexer.UNTIL)
                    {
                        if (currentBlock.Type == BlockType.Repeat)
                        {
                            currentBlock.SetEnd(token.GetEnd(version), token);
                            definitionContainer.ClearScope(currentBlock);
                            currentBlock = currentBlock.GetParent();
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.VAR)
                    {
                        if (tokens.Count - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER)
                        {
                            var variableDefinition = new DefinitionToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1], version);
                            definitionContainer.Add(currentBlock, variableDefinition);
                            currentBlock.AddToken(variableDefinition);
                            i += 1;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.IDENTIFIER || token.Type == RadAsm2Lexer.CLOSURE_IDENTIFIER)
                    {
                        var tokenText = token.GetText(version).TrimPrefix("#");
                        if (!TryAddInstruction(tokenText, token, currentBlock, version) &&
                            !TryAddReference(tokenText, token, currentBlock, version, definitionContainer, cancellation) &&
                            !TryAddBuiltinReference(tokenText, token, currentBlock, version))
                        {
                            referenceCandidates.Add((tokenText, token, currentBlock));
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.PP_INCLUDE)
                    {
                        if (tokens.Count - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.STRING_LITERAL)
                        {
                            await AddExternalDefinitionsAsync(document.Path, tokens[i + 1], currentBlock, definitionContainer);
                            i += 1;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.PP_DEFINE)
                    {
                        if (tokens.Count - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER)
                        {
                            var macroDefinition = new DefinitionToken(RadAsmTokenType.PreprocessorMacro, tokens[i + 1], version);
                            definitionContainer.Add(currentBlock, macroDefinition);
                            currentBlock.AddToken(macroDefinition);
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
                        definitionContainer.Add(currentBlock, parameterDefinition);
                        currentBlock.AddToken(parameterDefinition);
                    }
                }
            }

            foreach (var (text, trackingToken, block) in referenceCandidates)
            {
                if (!TryAddReference(text, trackingToken, block, version, definitionContainer, cancellation) && CheckInstructionDefinedInOtherSetsError(text, out var error))
                    errors.Add(new ErrorToken(trackingToken, version, error));
            }

            return new ParserResult(blocks, errors);
        }

        private enum ParserState
        {
            SearchInScope = 1,
            SearchArguments = 2,
            SearchArgAttribute = 3,
        }
    }
}
