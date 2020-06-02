using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser.Blocks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense
{
    [Export(typeof(NavigationTokenService))]
    internal class NavigationTokenService
    {
        private readonly RadeonServiceProvider _serviceProvider;
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        private readonly IVsTextManager _textManager;

        [ImportingConstructor]
        public NavigationTokenService(RadeonServiceProvider serviceProvider, DocumentAnalysisProvoder documentAnalysisProvoder)
        {
            _serviceProvider = serviceProvider;
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _textManager = serviceProvider.ServiceProvider.GetService(typeof(VsTextManagerClass)) as IVsTextManager;
        }

        public static TextExtent GetTextExtentOnCursor(ITextView view) =>
            view.Caret.Position.BufferPosition.GetExtent();

        public void GoToDefinition(ITextView view)
        {
            try
            {
                var extent = GetTextExtentOnCursor(view);
                var token = GetNaviationItem(extent, true);

                if (token != NavigationToken.Empty)
                    GoToPoint(new SnapshotPoint(token.Snapshot, token.GetEnd()));
            }
            catch (Exception e)
            {
                Error.LogError(e, "RadAsm navigation serivce");
            }
        }

        public void GoToPoint(SnapshotPoint point)
        {
            var buffer = _serviceProvider.EditorAdaptersFactoryService.GetBufferAdapter(point.Snapshot.TextBuffer);
            if (buffer == null)
                throw new InvalidOperationException("Cannot find IVsTextBuffer associated with point");

            _textManager.NavigateToPosition(buffer, VSConstants.LOGVIEWID.TextView_guid, point.Position, 0);
        }

        public NavigationToken GetNaviationItem(TextExtent extent, bool searchWithInclude = false)
        {
            try
            {
                return GetNaviationToken(extent, searchWithInclude);
            }
            catch (Exception e)
            {
                ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
                return NavigationToken.Empty;
            }
        }

        private NavigationToken GetNaviationToken(TextExtent extent, bool searchWithInclude)
        {
            if (!extent.IsSignificant)
                return NavigationToken.Empty;

            var text = extent.Span.GetText();
            var version = extent.Span.Snapshot;
            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(version.TextBuffer);

            if (documentAnalysis.LastParserResult.Count == 0)
                return NavigationToken.Empty;

            var currentBlock = documentAnalysis.LastParserResult.GetBlockBy(extent.Span.Start);

            if (currentBlock == null || currentBlock.Type == BlockType.Comment)
                return NavigationToken.Empty;

            if (FindNavigationTokenInBlock(version, currentBlock, text, out var token))
                return token;

            if (FindNavigationTokenInFunctionList(version, documentAnalysis.LastParserResult, text, out var functionToken))
                return functionToken;

            if (searchWithInclude && FindNavigationTokenInFileTree(documentAnalysis, text, out var fileToken))
                return fileToken;

            return NavigationToken.Empty;
        }

        private static bool FindNavigationTokenInBlock(ITextSnapshot version, IBlock currentBlock, string text, out NavigationToken outToken)
        {
            if (version.IsRadeonAsm2ContentType())
            {
                while (currentBlock != null)
                {
                    foreach (var token in currentBlock.Tokens)
                    {
                        if (token.Type == RadAsmTokenType.FunctionParameterReference)
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
            else if (version.IsRadeonAsmContentType())
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

                    foreach (var token in codeBlock.Tokens.Where(t => t.Type != RadAsmTokenType.Instruction))
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
                if (!File.Exists(pathToDocument))
                    continue;

                if (!VsShellUtilities.IsDocumentOpen(_serviceProvider.ServiceProvider, pathToDocument, Guid.Empty, out _, out _, out var windowFrame))
                {
                    VsShellUtilities.OpenDocument(_serviceProvider.ServiceProvider, pathToDocument, Guid.Empty, out _, out _, out windowFrame);
                }

                var view = VsShellUtilities.GetTextView(windowFrame);
                if (view.GetBuffer(out var lines) == 0)
                {
                    if (lines is IVsTextBuffer buffer)
                    {
                        var navigationBuffer = _serviceProvider.EditorAdaptersFactoryService.GetDataBuffer(buffer);
                        var includeDocumentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(navigationBuffer);

                        if (FindNavigationTokenInFunctionList(includeDocumentAnalysis.CurrentSnapshot, includeDocumentAnalysis.LastParserResult, text, out outToken))
                            return true;

                        if (FindNavigationTokenInRootBlock(includeDocumentAnalysis.CurrentSnapshot, includeDocumentAnalysis.LastParserResult[0], text, out outToken))
                            return true;
                    }
                }
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