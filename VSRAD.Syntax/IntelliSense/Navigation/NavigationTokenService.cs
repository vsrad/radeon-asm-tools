using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Helpers;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using VSRAD.Syntax.IntelliSense.Navigation;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.IntelliSense.Navigation.NavigationList;
using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.IntelliSense
{
    public interface INavigationTokenService
    {
        void GoToDefinition(ITextView view);
        void GoToPointOrOpenNavigationList(IReadOnlyList<NavigationToken> navigations);
        void GoToPoint(SnapshotPoint point);
        void GoToPoint(NavigationToken point);
        IReadOnlyList<NavigationToken> GetNaviationItem(TextExtent extent, bool searchWithInclude = false);
    }

    [Export(typeof(INavigationTokenService))]
    internal class NavigationTokenService : INavigationTokenService
    {
        private static List<NavigationToken> EmptyNavigations { get { return new List<NavigationToken>(); } }

        private readonly RadeonServiceProvider _serviceProvider;
        private readonly InstructionListManager _instructionListManager;
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        private readonly IVsTextManager _textManager;

        [ImportingConstructor]
        public NavigationTokenService(RadeonServiceProvider serviceProvider, InstructionListManager instructionListManager, DocumentAnalysisProvoder documentAnalysisProvoder)
        {
            _serviceProvider = serviceProvider;
            _instructionListManager = instructionListManager;
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _textManager = serviceProvider.ServiceProvider.GetService(typeof(VsTextManagerClass)) as IVsTextManager;
        }

        public void GoToDefinition(ITextView view)
        {
            try
            {
                var extent = view.GetTextExtentOnCursor();
                var tokens = GetNaviationItem(extent, true);

                GoToPointOrOpenNavigationList(tokens);
            }
            catch (Exception e)
            {
                Error.LogError(e, "RadAsm navigation serivce");
            }
        }

        public void GoToPointOrOpenNavigationList(IReadOnlyList<NavigationToken> navigations)
        {
            if (navigations.Count == 1)
            {
                GoToPoint(navigations[0]);
            }
            else if (navigations.Count > 1)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(() => NavigationList.UpdateNavigationListAsync(navigations));
            }
        }

        public void GoToPoint(SnapshotPoint point)
        {
            var buffer = _serviceProvider.EditorAdaptersFactoryService.GetBufferAdapter(point.Snapshot.TextBuffer);
            if (buffer == null)
            {
                if (_serviceProvider.TextDocumentFactoryService.TryGetTextDocument(point.Snapshot.TextBuffer, out var textDocument))
                {
                    if (!Utils.TryOpenDocument(_serviceProvider.ServiceProvider, textDocument.FilePath, out buffer))
                        throw new InvalidOperationException($"Cannot open IVsTextBuffer associated with {textDocument.FilePath}");
                }
                else
                {
                    throw new InvalidOperationException("Cannot find IVsTextBuffer associated with point");
                }
            }

            _textManager.NavigateToPosition(buffer, VSConstants.LOGVIEWID.TextView_guid, point.Position, 0);
        }

        public void GoToPoint(NavigationToken point)
        {
            if (point != NavigationToken.Empty)
                GoToPoint(new SnapshotPoint(point.Snapshot, point.GetEnd()));
        }

        public IReadOnlyList<NavigationToken> GetNaviationItem(TextExtent extent, bool searchWithInclude = false)
        {
            try
            {
                return GetNaviationToken(extent, searchWithInclude);
            }
            catch (Exception e)
            {
                Error.LogError(e, "RadAsm navigation serivce");
                return EmptyNavigations;
            }
        }

        private IReadOnlyList<NavigationToken> GetNaviationToken(TextExtent extent, bool searchWithInclude)
        {
            if (!extent.IsSignificant)
                return EmptyNavigations;

            var text = extent.Span.GetText();
            var version = extent.Span.Snapshot;
            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(version.TextBuffer);

            if (documentAnalysis.LexerTokenToRadAsmToken(documentAnalysis.GetToken(extent.Span.Start).Type) == RadAsmTokenType.Comment)
                return EmptyNavigations;

            if (documentAnalysis.LastParserResult.Count == 0)
                return EmptyNavigations;

            var currentBlock = documentAnalysis.LastParserResult.GetBlockBy(extent.Span.Start);

            if (currentBlock == null)
                return EmptyNavigations;

            var asmType = version.GetAsmType();
            if (_instructionListManager.TryGetInstructions(text, asmType, out var navigationTokens))
                return navigationTokens.ToList();

            if (FindNavigationTokenInBlock(version, asmType, currentBlock, text, out var token))
                return new List<NavigationToken>() { token };

            if (FindNavigationTokenInFunctionList(version, documentAnalysis.LastParserResult, text, out var functionToken))
                return new List<NavigationToken>() { functionToken };

            //if (searchWithInclude && FindNavigationTokenInFileTree(documentAnalysis, text, out var fileToken))
            //    return new List<NavigationToken>() { fileToken };

            return EmptyNavigations;
        }

        private static bool FindNavigationTokenInBlock(ITextSnapshot version, AsmType asmType, IBlock currentBlock, string text, out NavigationToken outToken)
        {
            if (asmType == AsmType.RadAsmDoc)
            {
                foreach (var token in currentBlock.Tokens)
                {
                    if (token.TrackingToken.GetText(version) == text)
                    {
                        outToken = new NavigationToken(token, version);
                        return true;
                    }
                }

                // skip find in function list in the documentation navigation
                outToken = NavigationToken.Empty;
                return true;
            }
            else if (asmType == AsmType.RadAsm2)
            {
                while (currentBlock != null)
                {
                    foreach (var token in currentBlock.Tokens)
                    {
                        if (token.Type == RadAsmTokenType.FunctionParameterReference
                            || token.Type == RadAsmTokenType.FunctionReference
                            || token.Type == RadAsmTokenType.LabelReference
                            || token.Type == RadAsmTokenType.Instruction)
                            continue;

                        if (token.TrackingToken.GetText(version) == text)
                        {
                            outToken = new NavigationToken(token, version);
                            return true;
                        }
                    }
                    currentBlock = currentBlock.Parrent;
                }
            }
            else if (asmType == AsmType.RadAsm)
            {
                var codeBlockStack = new Stack<IBlock>();
                while (currentBlock != null)
                {
                    if (currentBlock.Type == BlockType.Function)
                    {
                        var argToken = currentBlock
                            .Tokens
                            .FirstOrDefault(t => (t.Type == RadAsmTokenType.FunctionParameter) && ("\\" + t.TrackingToken.GetText(version)) == text);

                        if (argToken != null)
                        {
                            outToken = new NavigationToken(argToken, version);
                            return true;
                        }
                    }

                    codeBlockStack.Push(currentBlock);
                    currentBlock = currentBlock.Parrent;
                }

                while (codeBlockStack.Count != 0)
                {
                    var codeBlock = codeBlockStack.Pop();

                    foreach (var token in codeBlock.Tokens.Where(t => t.Type != RadAsmTokenType.Instruction && t.Type != RadAsmTokenType.FunctionReference && t.Type != RadAsmTokenType.LabelReference))
                    {
                        if (token.TrackingToken.GetText(version) == text)
                        {
                            outToken = new NavigationToken(token, version);
                            return true;
                        }
                    }
                }
            }

            outToken = NavigationToken.Empty;
            return false;
        }

        private static bool FindNavigationTokenInFunctionList(ITextSnapshot version, IReadOnlyList<IBlock> blocks, string text, out NavigationToken functionToken)
        {
            var functionTokens = blocks.GetFunctions().Select(b => b.Name);

            foreach (var token in functionTokens)
            {
                if (token.TrackingToken.GetText(version) == text)
                {
                    functionToken = new NavigationToken(token, version);
                    return true;
                }
            }

            functionToken = NavigationToken.Empty;
            return false;
        }

        private bool FindNavigationTokenInFileTree(DocumentAnalysis documentAnalysis, string text, out NavigationToken outToken)
        {
            outToken = NavigationToken.Empty;

            var textBuffer = documentAnalysis.CurrentSnapshot.TextBuffer;
            if (!textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var textDocument))
                return false;

            var dirPath = Path.GetDirectoryName(textDocument.FilePath);
            var includes = documentAnalysis
                .LastParserResult[0] // the first block is Root block
                .Tokens.Where(t => t.Type == RadAsmTokenType.Include);

            foreach (var include in includes)
            {
                var docFileName = include.TrackingToken.GetText(textBuffer.CurrentSnapshot).Trim('"');
                var extension = Path.GetExtension(docFileName);

                var pathToDocument = Path.GetFullPath(Path.Combine(dirPath, docFileName));
                if (!Utils.IsDocumentOpen(_serviceProvider.ServiceProvider, pathToDocument, out var buffer))
                {
                    if (!Utils.TryOpenDocument(_serviceProvider.ServiceProvider, pathToDocument, out buffer))
                        continue;
                }

                var navigationBuffer = _serviceProvider.EditorAdaptersFactoryService.GetDataBuffer(buffer);
                var includeDocumentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(navigationBuffer);

                if (FindNavigationTokenInFunctionList(includeDocumentAnalysis.CurrentSnapshot, includeDocumentAnalysis.LastParserResult, text, out outToken))
                    return true;

                if (FindNavigationTokenInRootBlock(includeDocumentAnalysis.CurrentSnapshot, includeDocumentAnalysis.LastParserResult[0], text, out outToken))
                    return true;
            }

            return false;
        }

        private static bool FindNavigationTokenInRootBlock(ITextSnapshot version, IBlock rootBlock, string text, out NavigationToken outToken)
        {
            outToken = NavigationToken.Empty;

            if (rootBlock.Type != BlockType.Root)
                return false;

            foreach (var token in rootBlock.Tokens)
            {
                if (token.Type == RadAsmTokenType.GlobalVariable || token.Type == RadAsmTokenType.Label)
                {
                    if (token.TrackingToken.GetText(version) == text)
                    {
                        outToken = new NavigationToken(token, version);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}