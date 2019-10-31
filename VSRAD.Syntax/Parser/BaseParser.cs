using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace VSRAD.Syntax.Parser
{
    public interface IBaseParser
    {
        ITextSnapshot CurrentSnapshot { get; }
        IBaseBlock RootBlock { get; }
        IList<IBaseBlock> ListBlock { get; }

        void Parse(object threadContext);
        IBaseBlock GetBlockBySnapshotPoint(SnapshotPoint point);
        IBaseBlock GetBlockByToken(IBaseToken token);
        FunctionBlock GetFunctionByLine(ITextSnapshotLine line);
        IList<IBaseBlock> GetFunctionBlocks();
    }

    internal class BaseParser : IBaseParser
    {
        private readonly IParserManager parserManager;
        private readonly ITextSnapshot currentSnapshot;
        private int indexStartLine;
        private RootBlock currentRootBlock;
        private IBaseBlock currentTreeBlock;
        private bool startFindManyLineDeclorationEnd;
        private List<IBaseToken> argumentTokens;
        private ITextSnapshotLine currentLine;
        private FunctionToken currentFunctionToken;
        private IList<IBaseBlock> currentListBlock;

        public ITextSnapshot CurrentSnapshot { get; private set; }
        public IBaseBlock RootBlock { get; private set; }
        public IList<IBaseBlock> ListBlock { get; private set; }

        public BaseParser(IParserManager parserManager, ITextSnapshot textSnapshot)
        {
            this.parserManager = parserManager;
            this.currentSnapshot = textSnapshot;
            this.indexStartLine = 0;
            this.currentRootBlock = new RootBlock(textSnapshot);
            this.currentTreeBlock = currentRootBlock;
            this.currentLine = null;
            this.startFindManyLineDeclorationEnd = false;
            this.argumentTokens = new List<IBaseToken>();
            this.currentListBlock = new List<IBaseBlock>();
        }

        public void Parse(object threadContext)
        {
            try
            {
                ParseStart(threadContext);
            }
            catch (Exception e)
            {
                ActivityLog.LogError(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
        }

        private void ParseStart(object threadContext)
        {
            CancellationToken token = (CancellationToken)threadContext;
            int startManyLineCommentIndex = 0;
            bool currentLineIsManyLineComment = false;

            foreach (var line in currentSnapshot.Lines)
            {
                if (token.IsCancellationRequested)
                    return;

                currentLine = line;
                var lineText = line.GetText();
                var cmpLineText = lineText.TrimStart();
                try
                {
                    if (currentLineIsManyLineComment)
                    {
                        if (cmpLineText.Contains(parserManager.ManyLineCommentEndPattern))
                        {
                            currentLineIsManyLineComment = false;
                            var endIndex = indexStartLine + lineText.IndexOf(parserManager.ManyLineCommentEndPattern) + parserManager.ManyLineCommentEndPattern.Length;
                            var startCommentBlock = new SnapshotPoint(currentSnapshot, startManyLineCommentIndex);
                            var endCommentBlock = new SnapshotPoint(currentSnapshot, endIndex);
                            var newCommentBlock = new BaseBlock(currentTreeBlock, BlockType.Comment, startCommentBlock);
                            newCommentBlock.SetBlockReady(endCommentBlock, endCommentBlock);
                            if (newCommentBlock.BlockReady)
                            {
                                newCommentBlock.AddToken(newCommentBlock.BlockSpan, Tokens.TokenType.Comment);
                                currentTreeBlock.Children.Add(newCommentBlock);
                                currentListBlock.Add(newCommentBlock);
                            }
                        }
                    }
                    else
                    {
                        if (cmpLineText.StartsWith(parserManager.ManyLineCommentStartPattern))
                        {
                            currentLineIsManyLineComment = true;
                            var index = lineText.IndexOf(parserManager.ManyLineCommentStartPattern);
                            startManyLineCommentIndex = line.Start + index;
                            ParseBlocks(lineText.Substring(0, index));
                        }
                        else if (cmpLineText.Contains(parserManager.OneLineCommentPattern))
                        {
                            var index = lineText.IndexOf(parserManager.OneLineCommentPattern);
                            currentTreeBlock.AddToken(new SnapshotSpan(currentSnapshot, new Span(line.Start + index, line.Length - index)), Tokens.TokenType.Comment);
                            ParseBlocks(lineText.Substring(0, index));
                        }
                        else
                        {
                            ParseBlocks(lineText);
                        }
                    }
                }
                catch (Exception e)
                {
                    Microsoft.VisualStudio.Shell.ActivityLog.LogError("RadeonAsmSyntax - BaseParser", e.Message);
                }
                indexStartLine += line.LengthIncludingLineBreak;
            }

            this.RootBlock = currentRootBlock;
            this.ListBlock = currentListBlock;
            this.CurrentSnapshot = currentSnapshot;

            parserManager.UpdateParser(this);
        }

        private void ParseBlocks(string text)
        {
            var cmpText = text.TrimStart();

            if (startFindManyLineDeclorationEnd)
            {
                if (text.IndexOf(parserManager.DeclorationEndPattern) != -1)
                {
                    currentTreeBlock = currentTreeBlock.AddChildren(new FunctionBlock(currentTreeBlock, new SnapshotPoint(currentSnapshot, indexStartLine + text.Length), currentFunctionToken));
                    startFindManyLineDeclorationEnd = false;
                    var functionArgsText = text.Substring(0, text.IndexOf(parserManager.DeclorationEndPattern)).Split(new char[] { ' ', '\t', ',', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var functionArgText in functionArgsText)
                    {
                        var startIndex = text.IndexOf(functionArgText) + indexStartLine;
                        (currentTreeBlock as FunctionBlock)?.Tokens.Add(new ArgumentToken(new SnapshotSpan(currentSnapshot, startIndex, functionArgText.Length)));
                    }
                    ((List<IBaseToken>)(currentTreeBlock as FunctionBlock)?.Tokens)?.AddRange(argumentTokens);
                }
                else
                {
                    var functionArgsText = text.Split(new char[] { ' ', '\t', ',', '[', ']', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var functionArgText in functionArgsText)
                    {
                        var startIndex = text.IndexOf(functionArgText) + indexStartLine;
                        argumentTokens.Add(new ArgumentToken(new SnapshotSpan(currentSnapshot, startIndex, functionArgText.Length)));
                    }
                }
                return;
            }

            if (cmpText.StartsWith(parserManager.KeyWordFunctionPattern))
            {
                var functionMatch = parserManager.FunctionNameRegular.Match(text);
                var functionToken = new FunctionToken(new SnapshotSpan(currentSnapshot, indexStartLine + functionMatch.Groups[1].Index, functionMatch.Groups[1].Length));

                if (!parserManager.EnableManyLineDecloration)
                {
                    currentTreeBlock = currentTreeBlock.AddChildren(new FunctionBlock(currentTreeBlock, new SnapshotPoint(currentSnapshot, indexStartLine + text.Length), functionToken));
                    currentRootBlock.FunctionTokens.Add(functionToken);

                    var functionArgsText = text.Substring(functionMatch.Index + functionMatch.Length).Split(new char[] { ' ', '\t', ',', '=' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var functionArgText in functionArgsText)
                    {
                        var startIndex = text.IndexOf(functionArgText) + indexStartLine;
                        (currentTreeBlock as FunctionBlock)?.Tokens.Add(new ArgumentToken(new SnapshotSpan(currentSnapshot, startIndex, functionArgText.Length)));
                    }
                }
                else
                {
                    if (text.IndexOf(parserManager.DeclorationStartPattern) != -1)
                    {
                        if (text.IndexOf(parserManager.DeclorationEndPattern) != -1)
                        {
                            currentTreeBlock = currentTreeBlock.AddChildren(new FunctionBlock(currentTreeBlock, new SnapshotPoint(currentSnapshot, indexStartLine + text.Length), functionToken));
                            currentRootBlock.FunctionTokens.Add(functionToken);

                            var functionArgsText = text.Substring(functionMatch.Index + functionMatch.Length, text.IndexOf(parserManager.DeclorationEndPattern) - functionMatch.Index - functionMatch.Length).Split(new char[] { ' ', '\t', ',', '=', '[', ']', '(' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var functionArgText in functionArgsText)
                            {
                                var startIndex = text.IndexOf(functionArgText) + indexStartLine;
                                (currentTreeBlock as FunctionBlock)?.Tokens.Add(new ArgumentToken(new SnapshotSpan(currentSnapshot, startIndex, functionArgText.Length)));
                            }
                        }
                        else
                        {
                            currentFunctionToken = functionToken;
                            startFindManyLineDeclorationEnd = true;
                            var functionArgsText = text.Substring(functionMatch.Index + functionMatch.Length).Split(new char[] { ' ', '\t', ',', '=', '[', ']', '(', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                            argumentTokens.Clear();
                            foreach (var functionArgText in functionArgsText)
                            {
                                var startIndex = text.IndexOf(functionArgText) + indexStartLine;
                                argumentTokens.Add(new ArgumentToken(new SnapshotSpan(currentSnapshot, startIndex, functionArgText.Length)));
                            }
                            return;
                        }
                    }
                    else
                    {
                        currentTreeBlock = currentTreeBlock.AddChildren(new FunctionBlock(currentTreeBlock, functionToken.SymbolSpan.End, functionToken));
                        currentRootBlock.FunctionTokens.Add(functionToken);
                    }
                }
                return;
            }

            foreach (var variableDefinition in parserManager.VariableDefinitionRegulars)
            {
                if (cmpText.Contains(variableDefinition.Key))
                {
                    var variableMatch = variableDefinition.Value.Match(text);
                    if (variableMatch.Success)
                    {
                        var indexStart = indexStartLine + variableMatch.Groups[1].Index;
                        var varToken = new VariableToken(new SnapshotSpan(currentSnapshot, indexStart, variableMatch.Groups[1].Length));
                        currentTreeBlock.Tokens.Add(varToken);
                    }
                }
            }


            foreach (var startPattern in parserManager.KeyWordStartPatterns)
            {
                if (cmpText.StartsWith(startPattern))
                {
                    var blockActualStart = new SnapshotPoint(currentSnapshot, indexStartLine + text.IndexOf(startPattern) + startPattern.Length);
                    var blockStart = new SnapshotPoint(currentSnapshot, indexStartLine + text.Length);
                    currentTreeBlock = currentTreeBlock.AddChildren(BlockType.Loops, startBlock: blockStart, blockActualStart: blockActualStart);
                    return;
                }
            }

            foreach (var endPattern in parserManager.KeyWordEndPatterns)
            {
                if (cmpText.StartsWith(endPattern))
                {
                    var endBlock = new SnapshotPoint(currentSnapshot, indexStartLine + text.IndexOf(endPattern) + endPattern.Length);
                    currentTreeBlock.SetBlockReady(endBlock: endBlock, actualEndBlock: endBlock);
                    if (currentTreeBlock.BlockReady)
                        currentListBlock.Add(currentTreeBlock);
                    if (currentTreeBlock.Parrent != null)
                        currentTreeBlock = currentTreeBlock.Parrent;
                    return;
                }
            }

            foreach (var middlePattern in parserManager.KeyWordMiddlePatterns)
            {
                if (cmpText.StartsWith(middlePattern))
                {
                    currentTreeBlock.SetBlockReady(currentSnapshot.GetLineFromLineNumber(currentLine.LineNumber - 1).End, currentLine.Start);
                    if (currentTreeBlock.BlockReady)
                        currentListBlock.Add(currentTreeBlock);
                    if (currentTreeBlock.Parrent != null)
                    {
                        currentTreeBlock = currentTreeBlock.Parrent;
                        currentTreeBlock = currentTreeBlock.AddChildren(BlockType.Loops, new SnapshotPoint(currentSnapshot, indexStartLine + text.Length));
                        return;
                    }
                }
            }
        }

        public IList<IBaseBlock> GetFunctionBlocks()
        {
            return currentListBlock.Where(block => block.BlockType == BlockType.Function).ToList();
        }

        public FunctionBlock GetFunctionByLine(ITextSnapshotLine line)
        {
            return (FunctionBlock)currentListBlock
                    .Where(block =>
                    (block.BlockType == BlockType.Function) &&
                    ((block as FunctionBlock).FunctionToken.Line.Start <= line.End) &&
                    ((block as FunctionBlock).BlockSpan.End >= line.Start))
                    .FirstOrDefault();
        }

        public IBaseBlock GetBlockBySnapshotPoint(SnapshotPoint point)
        {
            if (currentSnapshot.Equals(point.Snapshot))
            {
                var currentBlock = currentListBlock.Where(block => (block.BlockType != BlockType.Root) && block.BlockActualSpan.Start <= point && block.BlockActualSpan.End >= point).FirstOrDefault();
                return currentBlock ?? RootBlock;
            }

            return null;
        }

        public IBaseBlock GetBlockByToken(IBaseToken token)
        {
            if (currentSnapshot.Equals(token.SymbolSpan.Snapshot))
            {
                var currentBlock = currentListBlock.Where(b => b.Tokens.Contains(token)).FirstOrDefault();
                return currentBlock ?? RootBlock;
            }

            return null;
        }
    }
}
