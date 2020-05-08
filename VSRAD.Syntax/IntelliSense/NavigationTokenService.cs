using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser.Blocks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Editor;
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
        public static NavigationTokenService Instance { get; protected set; }

        private readonly ITextDocumentFactoryService _textDocumentFactoryService;
        public readonly IPeekBroker PeekBroker;
        private readonly IContentTypeRegistryService _contentTypeRegistryService;
        private readonly IFileExtensionRegistryService _fileExtensionRegistryService;
        private readonly ITextSearchService2 _textSearchService;
        private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;
        private readonly IVsTextManager _textManager;

        [ImportingConstructor]
        public NavigationTokenService(
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
            IPeekBroker peekBroker,
            ITextDocumentFactoryService textDocumentFactoryService,
            IContentTypeRegistryService contentTypeRegistryService,
            IFileExtensionRegistryService fileExtensionRegistryService,
            ITextSearchService2 textSearchService,
            IVsEditorAdaptersFactoryService adaptersFactoryService)
        {
            this._textDocumentFactoryService = textDocumentFactoryService;
            this.PeekBroker = peekBroker;
            this._contentTypeRegistryService = contentTypeRegistryService;
            this._fileExtensionRegistryService = fileExtensionRegistryService;
            this._textSearchService = textSearchService;
            this._adaptersFactoryService = adaptersFactoryService;
            this._textManager = serviceProvider.GetService(typeof(VsTextManagerClass)) as IVsTextManager;
            Instance = this;
        }

        public void GoToDefinition(IWpfTextView view)
        {
            try
            {
                var extent = GetTextExtentOnCursor(view);

                var token = GetNaviationItem(view, extent, false);
                if (token != null)
                    NavigateToFunction(token);
            }
            catch (Exception e)
            {
                ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
        }

        public IBaseToken GetNaviationItem(IWpfTextView view, TextExtent extent, bool onlyCurrentFile = true)
        {
            try
            {
                return GetNaviationToken(view, extent, onlyCurrentFile);
            }
            catch (Exception e)
            {
                ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
                return null;
            }
        }

        private IBaseToken GetNaviationToken(IWpfTextView view, TextExtent extent, bool onlyCurrentFile)
        {
            if (!extent.IsSignificant)
                return null;

            var text = extent.Span.GetText();
            var parserManager = view.GetParserManager();
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

        public static TextExtent GetTextExtentOnCursor(IWpfTextView view) =>
            view.Caret.Position.BufferPosition.GetExtent();

        private static void NavigateToFunction(IBaseToken token)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (token == null) return;

            String path = token.FilePath;
            var openDoc = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;

            Guid logicalView = VSConstants.LOGVIEWID_Code;
            if (ErrorHandler.Failed(
              openDoc.OpenDocumentViaProject(path, ref logicalView, out _,
                out _, out _, out var frame))
                  || frame == null)
            {
                return;
            }
            frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out var docData);

            var buffer = docData as VsTextBuffer;
            if (buffer == null)
            {
                if (docData is IVsTextBufferProvider bufferProvider)
                {
                    ErrorHandler.ThrowOnFailure(bufferProvider.GetTextBuffer(out var lines));
                    buffer = lines as VsTextBuffer;
                    if (buffer == null)
                        return;
                }
                else
                    return;
            }

            IVsTextManager mgr = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(VsTextManagerClass)) as IVsTextManager;
            mgr.NavigateToLineAndColumn(buffer, ref logicalView, token.Line.LineNumber, 0, token.Line.LineNumber, 0);
        }

        public IWpfTextView GetWpfTextView()
        {
            _textManager.GetActiveView(1, null, out var textViewCurrent);
            return (textViewCurrent != null) ? _adaptersFactoryService.GetWpfTextView(textViewCurrent) : null;
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
            var textBuffer = parser.CurrentSnapshot.TextBuffer;
            var rc = textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var textDocument);
            if (!rc)
            {
                outToken = null;
                return false;
            }

            var dirPath = Path.GetDirectoryName(textDocument.FilePath);
            var contentType = _contentTypeRegistryService.GetContentType(Constants.RadeonAsmSyntaxContentType);

            var documentNames = _textSearchService.FindAll(new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length), "include\\s\"(.+)\"", FindOptions.UseRegularExpressions | FindOptions.SingleLine);

            foreach (var documentName in documentNames)
            {
                var docFileName = Regex.Match(documentName.GetText(), "\"(.+)\"").Groups[1].Value;
                var extension = Path.GetExtension(docFileName);

                var extensionsAsm1 = _fileExtensionRegistryService.GetExtensionsForContentType(contentType);
                if (extensionsAsm1.Contains(extension))
                {
                    var pathToDocument = Path.GetFullPath(Path.Combine(dirPath, docFileName));
                    var document = _textDocumentFactoryService.CreateAndLoadTextDocument(pathToDocument, contentType);
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