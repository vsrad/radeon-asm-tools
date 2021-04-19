using Microsoft.VisualStudio.Text;
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
    internal class Asm2Parser : AbstractCodeParser
    {
        private static HashSet<string> Instructions = new HashSet<string>();
        private static HashSet<string> OtherInstructions = new HashSet<string>();
        public static void UpdateInstructions(IInstructionListManager sender, AsmType asmType)
        {
            const AsmType currentAsmType = AsmType.RadAsm2;
            if ((asmType & currentAsmType) != currentAsmType) return;

            UpdateInstructions(sender, currentAsmType, ref Instructions, ref OtherInstructions);
        }

        public Asm2Parser(IDocumentFactory documentFactory)
            : base(documentFactory) { }

        public override async Task<IParserResult> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> trackingTokens, CancellationToken cancellation)
        {
            var tokens = trackingTokens
                .Where(t => t.Type != RadAsm2Lexer.WHITESPACE && t.Type != RadAsm2Lexer.LINE_COMMENT)
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
            var parenthCnt = 0;
            var searchInCondition = false;
            var preprocessBlock = false;

            blocks.Add(currentBlock);
            for (int i = 0; i < tokens.Length; i++)
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
                        TryAddInstruction(token.GetText(version), token, currentBlock, version, Instructions);
                }
                else if (parserState == ParserState.SearchInScope)
                {
                    if (token.Type == RadAsm2Lexer.BLOCK_COMMENT)
                    {
                        blocks.AppendBlock(new Block(currentBlock, BlockType.Comment, token, token, version));
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

                            // lookbehind search references to label
                            var labelReferences = _referenceCandidates
                                .Where(t => t.text == labelDefinition.GetText())
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
                    else if (token.Type == RadAsm2Lexer.FUNCTION || token.Type == RadAsm2Lexer.SHADER)
                    {
                        if (tokens.Length - i > 2 && tokens[i + 1].Type == RadAsm2Lexer.IDENTIFIER)
                        {
                            if (tokens[i + 2].Type == RadAsm2Lexer.EOL)
                            {
                                var funcDefinition = new FunctionToken(RadAsmTokenType.FunctionName, tokens[i + 1], version);
                                _definitionContainer.Add(currentBlock, funcDefinition);
                                currentBlock = blocks.AppendBlock(new FunctionBlock(currentBlock, BlockType.Function, token, version, funcDefinition));
                                currentBlock.SetStart(tokens[i + 1].GetEnd(version));
                                i += 1;
                            }
                            else if (tokens[i + 2].Type == RadAsm2Lexer.LPAREN)
                            {
                                var funcDefinition = new FunctionToken(RadAsmTokenType.FunctionName, tokens[i + 1], version);
                                _definitionContainer.Add(currentBlock, funcDefinition);
                                currentBlock = blocks.AppendBlock(new FunctionBlock(currentBlock, BlockType.Function, token, version, funcDefinition));
                                parserState = ParserState.SearchArguments;

                                parenthCnt = 1;
                                i += 2;
                            }
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.IF)
                    {
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Condition, token, version));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm2Lexer.ELSIF || token.Type == RadAsm2Lexer.ELSE)
                    {
                        if (tokens.Length > 2)
                        {
                            currentBlock.SetEnd(tokens[i - 1].Start.GetPosition(version), token, version);
                            _definitionContainer.ClearScope(currentBlock);
                            currentBlock = currentBlock.GetParent();

                            currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Condition, token, version));
                            searchInCondition = true;
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.FOR || token.Type == RadAsm2Lexer.WHILE)
                    {
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Loop, token, version));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm2Lexer.END)
                    {
                        if (currentBlock.Type == BlockType.Function || currentBlock.Type == BlockType.Condition || currentBlock.Type == BlockType.Loop)
                        {
                            currentBlock.SetEnd(token.GetEnd(version), token, version);
                            _definitionContainer.ClearScope(currentBlock);
                            currentBlock = currentBlock.GetParent();
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.REPEAT)
                    {
                        currentBlock = blocks.AppendBlock(new Block(currentBlock, BlockType.Repeat, token, version));
                        searchInCondition = true;
                    }
                    else if (token.Type == RadAsm2Lexer.UNTIL)
                    {
                        if (currentBlock.Type == BlockType.Repeat)
                        {
                            currentBlock.SetEnd(token.GetEnd(version), token, version);
                            _definitionContainer.ClearScope(currentBlock);
                            currentBlock = currentBlock.GetParent();
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
                        if (!TryAddInstruction(tokenText, token, currentBlock, version, Instructions) && 
                            !TryAddReference(tokenText, token, currentBlock, version, cancellation))
                        {
                            _referenceCandidates.AddFirst((tokenText, token, currentBlock));
                        }
                    }
                    else if (token.Type == RadAsm2Lexer.PP_INCLUDE)
                    {
                        if (tokens.Length - i > 1 && tokens[i + 1].Type == RadAsm2Lexer.STRING_LITERAL)
                        {
                            await AddExternalDefinitionsAsync(document.Path, tokens[i + 1], version, currentBlock);
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
            SearchArgAttribute = 3,
        }
    }
}
