using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(ContentTypeManager))]
    internal sealed class ContentTypeManager
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly IVsEditorAdaptersFactoryService _textEditorAdaptersFactoryService;
        private readonly IFileExtensionRegistryService _fileExtensionRegistryService;
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        private readonly DTE _dte;
        private readonly IContentType _asm1ContentType;
        private readonly IContentType _asm2ContentType;
        private IEnumerable<string> _asm1Extensions;
        private IEnumerable<string> _asm2Extensions;

        [ImportingConstructor]
        public ContentTypeManager(SVsServiceProvider serviceProvider,
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            IContentTypeRegistryService contentTypeRegistryService,
            IFileExtensionRegistryService fileExtensionRegistryService,
            OptionsProvider optionsEventProvider,
            DocumentAnalysisProvoder documentAnalysisProvoder)
        {
            _serviceProvider = serviceProvider;
            _textEditorAdaptersFactoryService = editorAdaptersFactoryService;
            _fileExtensionRegistryService = fileExtensionRegistryService;
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _dte = (DTE)serviceProvider.GetService(typeof(DTE));

            _dte.Events.WindowEvents.WindowActivated += OnChangeActivatedWindow;
            optionsEventProvider.OptionsUpdated += FileExtensionChanged;
            _asm1ContentType = contentTypeRegistryService.GetContentType(Constants.RadeonAsmSyntaxContentType);
            _asm2ContentType = contentTypeRegistryService.GetContentType(Constants.RadeonAsm2SyntaxContentType);
            _asm1Extensions = optionsEventProvider.Asm1FileExtensions;
            _asm2Extensions = optionsEventProvider.Asm2FileExtensions;
        }

        private void OnChangeActivatedWindow(Window GotFocus, Window _) =>
            UpdateWindowContentType(GotFocus);

        private void UpdateWindowContentType(Window window)
        {
            if (window == null || !window.Kind.Equals("Document", StringComparison.OrdinalIgnoreCase))
                return;

            var fullPath = Path.Combine(window.Document.Path, window.Document.Name);
            if (VsShellUtilities.IsDocumentOpen(_serviceProvider, fullPath, Guid.Empty, out _, out _, out var windowFrame))
            {
                var textView = VsShellUtilities.GetTextView(windowFrame);
                var wpfTextView = _textEditorAdaptersFactoryService.GetWpfTextView(textView);
                var fileExtension = Path.GetExtension(window.Document.Name);

                UpdateTextBufferContentType(wpfTextView.TextBuffer, fileExtension);
            }
        }

        public async Task ChangeRadeonExtensionsAsync(IEnumerable<string> asm1Extensions, IEnumerable<string> asm2Extensions)
        {
            _asm1Extensions = asm1Extensions;
            _asm2Extensions = asm2Extensions;
            try
            {
                ChangeExtensions(_asm1ContentType, asm1Extensions);
                ChangeExtensions(_asm2ContentType, asm2Extensions);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (_dte.ActiveDocument != null)
                    UpdateWindowContentType(_dte.ActiveDocument.ActiveWindow);
            }
            catch (InvalidOperationException e)
            {
                Error.ShowWarning(e);
            }
        }

        private void FileExtensionChanged(OptionsProvider optionsProvider) =>
            ThreadHelper.JoinableTaskFactory.RunAsync(() => ChangeRadeonExtensionsAsync(optionsProvider.Asm1FileExtensions, optionsProvider.Asm2FileExtensions));

        private void ChangeExtensions(IContentType contentType, IEnumerable<string> extensions)
        {
            foreach (var ext in _fileExtensionRegistryService.GetExtensionsForContentType(contentType))
                _fileExtensionRegistryService.RemoveFileExtension(ext);

            foreach (var ext in extensions)
                _fileExtensionRegistryService.AddFileExtension(ext, contentType);
        }

        private void UpdateTextBufferContentType(ITextBuffer textBuffer, string fileExtension)
        {
            if (textBuffer == null || textBuffer.ContentType == _asm1ContentType || textBuffer.ContentType == _asm2ContentType)
                return;

            if (_asm1Extensions.Contains(fileExtension))
                UpdateTextBufferContentType(textBuffer, _asm1ContentType);

            if (_asm2Extensions.Contains(fileExtension))
                UpdateTextBufferContentType(textBuffer, _asm2ContentType);
        }

        private void UpdateTextBufferContentType(ITextBuffer textBuffer, IContentType contentType)
        {
            if (textBuffer == null || contentType == null)
                return;

            textBuffer.ChangeContentType(contentType, null);
            _documentAnalysisProvoder.CreateDocumentAnalysis(textBuffer);
        }
    }
}
