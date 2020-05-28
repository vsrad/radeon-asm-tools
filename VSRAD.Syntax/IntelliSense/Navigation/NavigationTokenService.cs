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
                var token = GetNaviationItem(extent, false);

                if (token != null)
                    GoToPoint(new SnapshotPoint(view.TextSnapshot, token.TrackingToken.GetEnd(view.TextSnapshot)));
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

        public AnalysisToken GetNaviationItem(TextExtent extent, bool onlyCurrentFile = true)
        {
            try
            {
                return GetNaviationToken(extent, onlyCurrentFile);
            }
            catch (Exception e)
            {
                ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
                return AnalysisToken.Empty;
            }
        }

        private AnalysisToken GetNaviationToken(TextExtent extent, bool onlyCurrentFile)
        {
            if (!extent.IsSignificant)
                return AnalysisToken.Empty;

            var text = extent.Span.GetText();
            var version = extent.Span.Snapshot;
            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(version.TextBuffer);

            if (documentAnalysis.LastParserResult.Count == 0)
                return AnalysisToken.Empty;

            var currentBlock = documentAnalysis.LastParserResult.GetBlockBy(extent.Span.Start);

            if (currentBlock == null || currentBlock.Type == BlockType.Comment)
                return AnalysisToken.Empty;

            if (FindNavigationTokenInBlock(version, currentBlock, text, out var token))
                return token;

            if (FindNavigationTokenInFunctionList(version, documentAnalysis.LastParserResult, text, out var functionToken))
                return functionToken;

            //if (!onlyCurrentFile && FindNavigationTokenInFileTree(version, parser, text, out var fileToken))
            //    return fileToken;

            return AnalysisToken.Empty;
        }

        private static bool FindNavigationTokenInBlock(ITextSnapshot version, IBlock currentBlock, string text, out AnalysisToken outToken)
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
                            outToken = token;
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

                        if (argToken != default)
                        {
                            outToken = argToken;
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
                            outToken = token;
                            return true;
                        }
                    }
                }
            }

            outToken = AnalysisToken.Empty;
            return false;
        }

        private static bool FindNavigationTokenInFunctionList(ITextSnapshot version, IReadOnlyList<IBlock> blocks, string text, out AnalysisToken functionToken)
        {
            var functionTokens = blocks.GetFunctions().Select(b => b.Name);

            foreach (var token in functionTokens)
            {
                if (token.TrackingToken.GetText(version) == text)
                {
                    functionToken = token;
                    return true;
                }
            }

            functionToken = AnalysisToken.Empty;
            return false;
        }

        private bool FindNavigationTokenInFileTree(IBlock parser, string text, out AnalysisToken outToken)
        {
            // TODO rewrite broken code
            outToken = AnalysisToken.Empty;
            return false;

            //var textBuffer = parser.CurrentSnapshot.TextBuffer;
            //var rc = textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var textDocument);
            //if (!rc)
            //{
            //    outToken = null;
            //    return false;
            //}

            //var dirPath = Path.GetDirectoryName(textDocument.FilePath);
            //var contentType = _serviceProvider.ContentTypeRegistryService.GetContentType(Constants.RadeonAsmSyntaxContentType);

            //var documentNames = _serviceProvider.TextSearchService.FindAll(new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length), "include\\s\"(.+)\"", FindOptions.UseRegularExpressions | FindOptions.SingleLine);

            //foreach (var documentName in documentNames)
            //{
            //    var docFileName = Regex.Match(documentName.GetText(), "\"(.+)\"").Groups[1].Value;
            //    var extension = Path.GetExtension(docFileName);

            //    var extensionsAsm1 = _serviceProvider.FileExtensionRegistryService.GetExtensionsForContentType(contentType);
            //    if (extensionsAsm1.Contains(extension))
            //    {
            //        var pathToDocument = Path.GetFullPath(Path.Combine(dirPath, docFileName));
            //        var document = _serviceProvider.TextDocumentFactoryService.CreateAndLoadTextDocument(pathToDocument, contentType);
            //        var parserManager = document.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());

            //        parserManager.InitializeAsm1(document.TextBuffer);
            //        parserManager.ParseSync();

            //        var rootBlock = parserManager.ActualParser.RootBlock;
            //        var tokens = (rootBlock as RootBlock).FunctionTokens.Concat(rootBlock.Tokens);

            //        foreach (var token in tokens)
            //        {
            //            if (token.TokenName == text)
            //            {
            //                outToken = token;
            //                return true;
            //            }
            //        }
            //    }
            //}

            //outToken = null;
            //return false;
        }
    }
}