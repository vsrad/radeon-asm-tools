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
                        throw new InvalidOperationException($"Cannot open file associated with {textDocument.FilePath}");
                }
                else
                {
                    // if external definition parsed but the document was closed we should reopen document
                    if (!point.Snapshot.TextBuffer.Properties.TryGetProperty<DocumentInfo>(typeof(DocumentInfo), out var documentInfo))
                        throw new InvalidOperationException("Cannot determine the file that contains the navigation definition");
                    if (!Utils.TryOpenDocument(_serviceProvider.ServiceProvider, documentInfo.Path, out buffer))
                        throw new InvalidOperationException($"Cannot open file associated with {textDocument.FilePath}");
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

            var currentToken = GetCurrentToken(currentBlock, extent.Span);
            if (currentToken is ReferenceToken referenceToken)
                return new List<NavigationToken>() { new NavigationToken(referenceToken.Definition, referenceToken.Definition.TrackingToken.Start.TextBuffer.CurrentSnapshot) };

            var asmType = version.GetAsmType();
            if (_instructionListManager.TryGetInstructions(text, asmType, out var navigationTokens))
                return navigationTokens.ToList();

            if (FindNavigationTokenInBlock(version, asmType, currentBlock, text, out var token))
                return new List<NavigationToken>() { token };

            if (FindNavigationTokenInFunctionList(version, documentAnalysis.LastParserResult, text, out var functionToken))
                return new List<NavigationToken>() { functionToken };

            return EmptyNavigations;
        }

        private AnalysisToken GetCurrentToken(IBlock currentBlock, SnapshotSpan span) =>
            currentBlock.Tokens.FirstOrDefault(t => t.TrackingToken.GetSpan(span.Snapshot).Contains(span));

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
    }
}