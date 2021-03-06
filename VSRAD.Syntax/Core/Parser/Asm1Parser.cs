﻿using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.Parser
{
    internal class Asm1Parser : AbstractCodeParser
    {
        private static HashSet<string> Instructions = new HashSet<string>();
        private static HashSet<string> OtherInstructions = new HashSet<string>();
        public static void UpdateInstructions(IInstructionListManager sender, AsmType asmType)
        {
            const AsmType currentAsmType = AsmType.RadAsm;
            if ((asmType & currentAsmType) != currentAsmType) return;

            UpdateInstructions(sender, currentAsmType, ref Instructions, ref OtherInstructions);
        }

        public Asm1Parser(IDocumentFactory documentFactory)
            : base(documentFactory) { }

        public override async Task<IParserResult> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            var tokens = trackingTokens
                .Where(t => t.Type != RadAsmLexer.WHITESPACE && t.Type != RadAsmLexer.LINE_COMMENT)
                .AsParallel()
                .AsOrdered()
                .WithCancellation(cancellation)
                .ToArray();

            _referenceCandidates.Clear();
            _definitionContainer.Clear();
            var blocks = new List<IBlock>();
            var errors = new List<IErrorToken>();
            var currentBlock = new Block();
            var parserState = ParserState.SearchInScope;
            var searchInCondition = false;
            var preprocessBlock = false;

            blocks.Add(currentBlock);
            for (var i = 0; i < tokens.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var token = tokens[i];

                if (token.Type == RadAsmLexer.PP_ELSE || token.Type == RadAsmLexer.PP_ELSIF || token.Type == RadAsmLexer.PP_ELIF)
                {
                    preprocessBlock = true;
                }
                else if (token.Type == RadAsmLexer.PP_ENDIF)
                {
                    preprocessBlock = false;
                }

                if (preprocessBlock)
                {
                    if (token.Type == RadAsmLexer.IDENTIFIER)
                        TryAddInstruction(token.GetText(version), token, currentBlock, version, Instructions);
                }
                else if (parserState == ParserState.SearchInScope)
                {
                    if (token.Type == RadAsmLexer.BLOCK_COMMENT)
                    {
                        blocks.AppendBlock(new Block(currentBlock, BlockType.Comment, token, token, version));
                    }
                    else if (token.Type == RadAsmLexer.MACRO)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmLexer.IDENTIFIER)
                        {
                            var funcDefinition = new FunctionToken(RadAsmTokenType.FunctionName, tokens[i + 1], version);
                            _definitionContainer.Add(currentBlock, funcDefinition);
                            currentBlock = blocks.AppendBlock(new FunctionBlock(currentBlock, BlockType.Function, token, version, funcDefinition));
                            parserState = ParserState.SearchArguments;
                            i += 1;
                        }
                    }
                    else if (token.Type == RadAsmLexer.ENDM && currentBlock.Type == BlockType.Function)
                    {
                        currentBlock.SetEnd(token.GetEnd(version), token, version);
                        _definitionContainer.ClearScope(currentBlock);
                        currentBlock = currentBlock.GetParent();

                        parserState = ParserState.SearchInScope;
                    }
                    else if (token.Type == RadAsmLexer.EOL)
                    {
                        if (searchInCondition)
                        {
                            currentBlock.SetStart(tokens[i - 1].GetEnd(version));
                            searchInCondition = false;
                        }

                        if (tokens.Length - i > 3
                            && tokens[i + 1].Type == RadAsmLexer.IDENTIFIER
                            && tokens[i + 2].Type == RadAsmLexer.COLON
                            && tokens[i + 3].Type == RadAsmLexer.EOL)
                        {
                            var labelDefinition = new DefinitionToken(RadAsmTokenType.Label, tokens[i + 1], version);
                            _definitionContainer.Add(currentBlock, labelDefinition);
                            currentBlock.AddToken(labelDefinition);

                            // lookbehind search references to label
                            var labelReferences = _referenceCandidates
                                .Where(t => t.text == labelDefinition.GetText())
                                .Reverse()
                                .TakeWhile(t => currentBlock.Area.Contains(t.block.Area.Start))
                                .ToList();

                            foreach (var reference in labelReferences)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                _referenceCandidates.Remove(reference);
                                reference.block.AddToken(new ReferenceToken(RadAsmTokenType.LabelReference, reference.trackingToken, version, labelDefinition));
                            }
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
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Condition, token, version));
                        searchInCondition = true;
                    }
                    else if ((token.Type == RadAsmLexer.ELSEIF || token.Type == RadAsmLexer.ELSE) && currentBlock.Type == BlockType.Condition)
                    {
                        currentBlock.SetEnd(tokens[i - 1].Start.GetPosition(version), token, version);
                        _definitionContainer.ClearScope(currentBlock);
                        currentBlock = currentBlock.GetParent();

                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Condition, token, version));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsmLexer.ENDIF && currentBlock.Type == BlockType.Condition)
                    {
                        currentBlock.SetEnd(token.GetEnd(version), token, version);
                        _definitionContainer.ClearScope(currentBlock);
                        currentBlock = currentBlock.GetParent();
                    }
                    else if (token.Type == RadAsmLexer.REPT
                        || token.Type == RadAsmLexer.IRP
                        || token.Type == RadAsmLexer.IRPC)
                    {
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Repeat, token, version));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsmLexer.ENDR && currentBlock.Type == BlockType.Repeat)
                    {
                        currentBlock.SetEnd(token.GetEnd(version), token, version);
                        _definitionContainer.ClearScope(currentBlock);
                        currentBlock = currentBlock.GetParent();
                    }
                    else if (token.Type == RadAsmLexer.SET)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmLexer.IDENTIFIER)
                        {
                            var variableDefinition = (tokens.Length - i > 3 && tokens[i + 2].Type == RadAsmLexer.COMMA && tokens[i + 3].Type == RadAsmLexer.CONSTANT)
                                ? new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1], version, tokens[i + 3])
                                : new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1], version);
                            _definitionContainer.Add(currentBlock, variableDefinition);
                            currentBlock.AddToken(variableDefinition);
                        }
                    }
                    else if (token.Type == RadAsmLexer.IDENTIFIER)
                    {
                        var tokenText = token.GetText(version);
                        if (!TryAddInstruction(tokenText, token, currentBlock, version, Instructions) &&
                            !TryAddReference(tokenText, token, currentBlock, version, cancellation))
                        {
                            if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmLexer.EQ)
                            {
                                var variableDefinition = new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, token, version);
                                _definitionContainer.Add(currentBlock, variableDefinition);
                                currentBlock.AddToken(variableDefinition);
                            }
                            else
                            {
                                _referenceCandidates.AddLast((tokenText, token, currentBlock));
                            }
                        }
                    }
                    else if (token.Type == RadAsmLexer.INCLUDE
                        || token.Type == RadAsmLexer.PP_INCLUDE)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsmLexer.STRING_LITERAL)
                        {
                            await AddExternalDefinitionsAsync(document.Path, tokens[i + 1], version, currentBlock);
                            i += 1;
                        }
                    }
                }
                else if (parserState == ParserState.SearchArguments)
                {
                    if (token.Type == RadAsmLexer.EOL)
                    {
                        currentBlock.SetStart(tokens[i - 1].GetEnd(version));
                        parserState = ParserState.SearchInScope;
                        continue;
                    }

                    if (token.Type == RadAsmLexer.IDENTIFIER)
                    {
                        var functionDefinition = new DefinitionToken(RadAsmTokenType.FunctionParameter, token, version);
                        _definitionContainer.Add(currentBlock, functionDefinition, $"\\{functionDefinition.GetText()}");
                        currentBlock.AddToken(functionDefinition);
                    }
                }
            }

            foreach (var (text, trackingToken, block) in _referenceCandidates)
            {
                if (!TryAddReference(text, trackingToken, block, version, cancellation) && OtherInstructions.Contains(text))
                    errors.Add(new ErrorToken(trackingToken, version, ErrorMessages.InvalidInstructionSetErrorMessage));
            }

            return new ParserResult(blocks, errors);
        }

        private enum ParserState
        {
            SearchInScope = 1,
            SearchArguments = 2,
        }
    }
}
