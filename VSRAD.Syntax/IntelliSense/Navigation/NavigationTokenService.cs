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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace VSRAD.Syntax.IntelliSense
{
    [Export(typeof(NavigationTokenService))]
    internal class NavigationTokenService
    {
        private readonly RadeonServiceProvider _editorService;
        private readonly IVsTextManager _textManager;

        [ImportingConstructor]
        public NavigationTokenService(RadeonServiceProvider editorService)
        {
            _editorService = editorService;
            _textManager = editorService.ServiceProvider.GetService(typeof(VsTextManagerClass)) as IVsTextManager;
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
                    GoToPoint(token.SymbolSpan.Start);
            }
            catch (Exception e)
            {
                ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
        }

        public void GoToPoint(SnapshotPoint point)
        {
            var buffer = _editorService.EditorAdaptersFactoryService.GetBufferAdapter(point.Snapshot.TextBuffer);
            if (buffer == null)
                throw new InvalidOperationException("Cannot find IVsTextBuffer associated with point");

            _textManager.NavigateToPosition(buffer, VSConstants.LOGVIEWID.TextView_guid, point.Position, 0);
        }

        public IBaseToken GetNaviationItem(TextExtent extent, bool onlyCurrentFile = true)
        {
            try
            {
                return GetNaviationToken(extent, onlyCurrentFile);
            }
            catch (Exception e)
            {
                ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
                return null;
            }
        }

        private IBaseToken GetNaviationToken(TextExtent extent, bool onlyCurrentFile)
        {
            if (!extent.IsSignificant)
                return null;

            var text = extent.Span.GetText();
            var parserManager = extent.Span.Snapshot.TextBuffer.GetParserManager();
            var parser = parserManager.ActualParser;

            if (parser == null)
                return null;

            var currentBlock = parser.GetBlockBySnapshotPoint(extent.Span.Start);

            if (currentBlock == null)
                return null;

            if (FindNavigationTokenInBlock(currentBlock, text, out var token))
                return token;

            if (FindNavigationTokenInFunctionList(parser, text, out var functionToken))
                return functionToken;

            if (!onlyCurrentFile && FindNavigationTokenInFileTree(parser, text, out var fileToken))
                return fileToken;

            return null;
        }

        private static bool FindNavigationTokenInBlock(IBaseBlock currentBlock, string text, out IBaseToken outToken)
        {
            if (currentBlock.IsRadeonAsm2ContentType())
            {
                while (currentBlock != null)
                {
                    foreach (var token in currentBlock.Tokens)
                    {
                        if (token.TokenName == text)
                        {
                            outToken = token;
                            return true;
                        }
                    }
                    currentBlock = currentBlock.Parrent;
                }

                outToken = null;
                return false;
            }

            if (currentBlock.IsRadeonAsmContentType())
            {
                var codeBlockStack = new Stack<IBaseBlock>();

                while (currentBlock != null)
                {
                    var argToken = currentBlock.Tokens.FirstOrDefault(token => (token.TokenType == TokenType.Argument) && token.TokenName == text);
                    if (argToken != null)
                    {
                        outToken = argToken;
                        return true;
                    }

                    codeBlockStack.Push(currentBlock);
                    currentBlock = currentBlock.Parrent;
                }

                while (codeBlockStack.Count != 0)
                {
                    var codeBlock = codeBlockStack.Pop();

                    foreach (var token in codeBlock.Tokens)
                    {
                        if (token.TokenName == text)
                        {
                            outToken = token;
                            return true;
                        }
                    }
                }
            }

            outToken = null;
            return false;
        }

        private static bool FindNavigationTokenInFunctionList(IBaseParser parser, string text, out IBaseToken functionToken)
        {
            var functionTokens = (parser.RootBlock as RootBlock).FunctionTokens;

            foreach (var token in functionTokens)
            {
                if (token.TokenName == text)
                {
                    functionToken = token;
                    return true;
                }
            }

            functionToken = null;
            return false;
        }

        private bool FindNavigationTokenInFileTree(IBaseParser parser, string text, out IBaseToken outToken)
        {
            // TODO rewrite broken code
            outToken = null;
            return false;

            var textBuffer = parser.CurrentSnapshot.TextBuffer;
            var rc = textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var textDocument);
            if (!rc)
            {
                outToken = null;
                return false;
            }

            var dirPath = Path.GetDirectoryName(textDocument.FilePath);
            var contentType = _editorService.ContentTypeRegistryService.GetContentType(Constants.RadeonAsmSyntaxContentType);

            var documentNames = _editorService.TextSearchService.FindAll(new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length), "include\\s\"(.+)\"", FindOptions.UseRegularExpressions | FindOptions.SingleLine);

            foreach (var documentName in documentNames)
            {
                var docFileName = Regex.Match(documentName.GetText(), "\"(.+)\"").Groups[1].Value;
                var extension = Path.GetExtension(docFileName);

                var extensionsAsm1 = _editorService.FileExtensionRegistryService.GetExtensionsForContentType(contentType);
                if (extensionsAsm1.Contains(extension))
                {
                    var pathToDocument = Path.GetFullPath(Path.Combine(dirPath, docFileName));
                    var document = _editorService.TextDocumentFactoryService.CreateAndLoadTextDocument(pathToDocument, contentType);
                    var parserManager = document.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());

                    parserManager.InitializeAsm1(document.TextBuffer);
                    parserManager.ParseSync();

                    var rootBlock = parserManager.ActualParser.RootBlock;
                    var tokens = (rootBlock as RootBlock).FunctionTokens.Concat(rootBlock.Tokens);

                    foreach (var token in tokens)
                    {
                        if (token.TokenName == text)
                        {
                            outToken = token;
                            return true;
                        }
                    }
                }
            }

            outToken = null;
            return false;
        }
    }
}