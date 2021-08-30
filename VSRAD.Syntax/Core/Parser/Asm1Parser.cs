using System;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.Parser
{
    internal sealed class Asm1Parser : AbstractCodeParser
    {
        public static IParser Instance => LazyInstance.Value;

        private static readonly Lazy<IParser> LazyInstance = new Lazy<IParser>(() =>
        {
            var serviceProvider = ServiceProvider.GlobalProvider;
            var documentFactory = serviceProvider.GetMefService<IDocumentFactory>();
            var instructionListManager = serviceProvider.GetMefService<IInstructionListManager>();

            return new Asm1Parser(documentFactory, instructionListManager);
        });

        private Asm1Parser(IDocumentFactory documentFactory, IInstructionListManager instructionListManager) 
            : base(documentFactory, instructionListManager, AsmType.RadAsm) { }

        public override Task<IParserResult> RunAsync(IDocument document, ITextSnapshot version,
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

        private async Task<IParserResult> ParseAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            var tokens = trackingTokens
                .Where(t => t.Type != RadAsm1Lexer.WHITESPACE && t.Type != RadAsm1Lexer.LINE_COMMENT)
                .AsParallel()
                .AsOrdered()
                .WithCancellation(cancellation)
                .ToArray();

            var definitionContainer = new DefinitionContainer();
            var referenceCandidates = new LinkedList<(string text, TrackingToken trackingToken, IBlock block)>();

            var blocks = new List<IBlock>();
            var errors = new List<IErrorToken>();
            IBlock currentBlock = new Block(version);
            var parserState = ParserState.SearchInScope;
            var searchInCondition = false;
            var preprocessBlock = false;

            blocks.Add(currentBlock);
            for (var i = 0; i < tokens.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var token = tokens[i];

                if (token.Type == RadAsm1Lexer.PP_ELSE || token.Type == RadAsm1Lexer.PP_ELSIF || token.Type == RadAsm1Lexer.PP_ELIF)
                {
                    preprocessBlock = true;
                }
                else if (token.Type == RadAsm1Lexer.PP_ENDIF)
                {
                    preprocessBlock = false;
                }

                if (preprocessBlock)
                {
                    if (token.Type == RadAsm1Lexer.IDENTIFIER)
                        TryAddInstruction(token.GetText(version), token, currentBlock, version);

                    continue;
                }
                else if (parserState == ParserState.SearchInScope)
                {
                    if (token.Type == RadAsm1Lexer.BLOCK_COMMENT)
                    {
                        blocks.AppendBlock(new Block(currentBlock, BlockType.Comment, token, token));
                    }
                    else if (token.Type == RadAsm1Lexer.MACRO)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm1Lexer.IDENTIFIER)
                        {
                            var funcDefinition = new DefinitionToken(RadAsmTokenType.FunctionName, tokens[i + 1], version);
                            definitionContainer.Add(currentBlock, funcDefinition);
                            currentBlock = blocks.AppendBlock(new FunctionBlock(currentBlock, BlockType.Function, token, funcDefinition));
                            parserState = ParserState.SearchArguments;
                            i += 1;
                        }
                    }
                    else if (token.Type == RadAsm1Lexer.ENDM && currentBlock.Type == BlockType.Function)
                    {
                        currentBlock.SetEnd(token.GetEnd(version), token);
                        definitionContainer.ClearScope(currentBlock);
                        currentBlock = currentBlock.GetParent();

                        parserState = ParserState.SearchInScope;
                    }
                    else if (token.Type == RadAsm1Lexer.EOL)
                    {
                        if (searchInCondition)
                        {
                            currentBlock.SetStart(tokens[i - 1].GetEnd(version));
                            searchInCondition = false;
                        }

                        if (tokens.Length - i > 3
                            && tokens[i + 1].Type == RadAsm1Lexer.IDENTIFIER
                            && tokens[i + 2].Type == RadAsm1Lexer.COLON
                            && tokens[i + 3].Type == RadAsm1Lexer.EOL)
                        {
                            var labelDefinition = new DefinitionToken(RadAsmTokenType.Label, tokens[i + 1], version);
                            definitionContainer.Add(currentBlock, labelDefinition);
                            currentBlock.AddToken(labelDefinition);
                            i += 2;
                        }
                    }
                    else if (token.Type == RadAsm1Lexer.IF
                        || token.Type == RadAsm1Lexer.IFDEF
                        || token.Type == RadAsm1Lexer.IFNOTDEF
                        || token.Type == RadAsm1Lexer.IFB
                        || token.Type == RadAsm1Lexer.IFC
                        || token.Type == RadAsm1Lexer.IFEQ
                        || token.Type == RadAsm1Lexer.IFEQS
                        || token.Type == RadAsm1Lexer.IFGE
                        || token.Type == RadAsm1Lexer.IFGT
                        || token.Type == RadAsm1Lexer.IFLE
                        || token.Type == RadAsm1Lexer.IFLT
                        || token.Type == RadAsm1Lexer.IFNB
                        || token.Type == RadAsm1Lexer.IFNC
                        || token.Type == RadAsm1Lexer.IFNE
                        || token.Type == RadAsm1Lexer.IFNES)
                    {
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Condition, token));
                        searchInCondition = true;
                    }
                    else if ((token.Type == RadAsm1Lexer.ELSEIF || token.Type == RadAsm1Lexer.ELSE) && currentBlock.Type == BlockType.Condition)
                    {
                        currentBlock.SetEnd(tokens[i - 1].Start.GetPosition(version), token);
                        definitionContainer.ClearScope(currentBlock);
                        currentBlock = currentBlock.GetParent();

                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Condition, token));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm1Lexer.ENDIF && currentBlock.Type == BlockType.Condition)
                    {
                        currentBlock.SetEnd(token.GetEnd(version), token);
                        definitionContainer.ClearScope(currentBlock);
                        currentBlock = currentBlock.GetParent();
                    }
                    else if (token.Type == RadAsm1Lexer.REPT
                        || token.Type == RadAsm1Lexer.IRP
                        || token.Type == RadAsm1Lexer.IRPC)
                    {
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Repeat, token));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm1Lexer.ENDR && currentBlock.Type == BlockType.Repeat)
                    {
                        currentBlock.SetEnd(token.GetEnd(version), token);
                        definitionContainer.ClearScope(currentBlock);
                        currentBlock = currentBlock.GetParent();
                    }
                    else if (token.Type == RadAsm1Lexer.SET)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm1Lexer.IDENTIFIER)
                        {
                            var variableDefinition = (tokens.Length - i > 3 && tokens[i + 2].Type == RadAsm1Lexer.COMMA && tokens[i + 3].Type == RadAsm1Lexer.CONSTANT)
                                ? new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1], version, tokens[i + 3])
                                : new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, tokens[i + 1], version);
                            definitionContainer.Add(currentBlock, variableDefinition);
                            currentBlock.AddToken(variableDefinition);
                        }
                    }
                    else if (token.Type == RadAsm1Lexer.IDENTIFIER)
                    {
                        var tokenText = token.GetText(version);
                        if (!TryAddInstruction(tokenText, token, currentBlock, version) && 
                            !TryAddReference(tokenText, token, currentBlock, version, definitionContainer, cancellation))
                        {
                            if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm1Lexer.EQ)
                            {
                                var variableDefinition = new VariableToken(currentBlock.Type == BlockType.Root ? RadAsmTokenType.GlobalVariable : RadAsmTokenType.LocalVariable, token, version);
                                definitionContainer.Add(currentBlock, variableDefinition);
                                currentBlock.AddToken(variableDefinition);
                            }
                            else
                            {
                                referenceCandidates.AddLast((tokenText, token, currentBlock));
                            }
                        }
                    }
                    else if (token.Type == RadAsm1Lexer.INCLUDE
                        || token.Type == RadAsm1Lexer.PP_INCLUDE)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm1Lexer.STRING_LITERAL)
                        {
                            await AddExternalDefinitionsAsync(document.Path, tokens[i + 1], currentBlock, definitionContainer);
                            i += 1;
                        }
                    }
                }
                else if (parserState == ParserState.SearchArguments)
                {
                    if (token.Type == RadAsm1Lexer.EOL)
                    {
                        currentBlock.SetStart(tokens[i - 1].GetEnd(version));
                        parserState = ParserState.SearchInScope;
                        continue;
                    }

                    if (token.Type == RadAsm1Lexer.IDENTIFIER)
                    {
                        var functionDefinition = new DefinitionToken(RadAsmTokenType.FunctionParameter, token, version);
                        definitionContainer.Add(currentBlock, functionDefinition, $"\\{functionDefinition.GetText()}");
                        currentBlock.AddToken(functionDefinition);
                    }
                }
            }

            foreach (var (text, trackingToken, block) in referenceCandidates)
            {
                if (!TryAddReference(text, trackingToken, block, version, definitionContainer, cancellation) && OtherInstructions.Contains(text))
                    errors.Add(new ErrorToken(trackingToken, version, ErrorMessages.InvalidInstructionSetErrorMessage));
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
