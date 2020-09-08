using EnvDTE;
using Microsoft.VisualStudio;
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
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(ContentTypeManager))]
    internal sealed class ContentTypeManager
    {
        public readonly IContentType Asm1ContentType;
        public readonly IContentType Asm2ContentType;
        public readonly IContentType AsmDocContentType;

        private readonly SVsServiceProvider _serviceProvider;
        private readonly IVsEditorAdaptersFactoryService _textEditorAdaptersFactoryService;
        private readonly IFileExtensionRegistryService _fileExtensionRegistryService;
        private readonly DTE _dte;
        private IEnumerable<string> _asm1Extensions;
        private IEnumerable<string> _asm2Extensions;
        private readonly List<string> _asmDocExtensions;

        [ImportingConstructor]
        public ContentTypeManager(SVsServiceProvider serviceProvider,
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            IContentTypeRegistryService contentTypeRegistryService,
            IFileExtensionRegistryService fileExtensionRegistryService,
            OptionsProvider optionsEventProvider)
        {
            _serviceProvider = serviceProvider;
            _textEditorAdaptersFactoryService = editorAdaptersFactoryService;
            _fileExtensionRegistryService = fileExtensionRegistryService;
            _dte = (DTE)serviceProvider.GetService(typeof(DTE));

            _dte.Events.WindowEvents.WindowActivated += OnChangeActivatedWindow;
            optionsEventProvider.OptionsUpdated += FileExtensionChanged;
            Asm1ContentType = contentTypeRegistryService.GetContentType(Constants.RadeonAsmSyntaxContentType);
            Asm2ContentType = contentTypeRegistryService.GetContentType(Constants.RadeonAsm2SyntaxContentType);
            AsmDocContentType = contentTypeRegistryService.GetContentType(Constants.RadeonAsmDocumentationContentType);
            _asm1Extensions = optionsEventProvider.Asm1FileExtensions;
            _asm2Extensions = optionsEventProvider.Asm2FileExtensions;
            _asmDocExtensions = new List<string>() { Constants.FileExtensionAsm1Doc, Constants.FileExtensionAsm2Doc };
        }

        private void OnChangeActivatedWindow(Window GotFocus, Window _) =>
            UpdateWindowContentType(GotFocus);

        public IContentType DetermineContentType(string path)
        {
            var fileExtension = Path.GetExtension(path);
            if (_asm1Extensions.Contains(fileExtension))
                return Asm1ContentType;

            if (_asm2Extensions.Contains(fileExtension))
                return Asm2ContentType;

            if (_asmDocExtensions.Contains(fileExtension))
                return AsmDocContentType;

            return null;
        }

        private void UpdateWindowContentType(Window window)
        {
            if (window == null || !window.Kind.Equals("Document", StringComparison.OrdinalIgnoreCase))
                return;

            var fullPath = Path.Combine(window.Document.Path, window.Document.Name);
            if (VsShellUtilities.IsDocumentOpen(_serviceProvider, fullPath, Guid.Empty, out _, out _, out var windowFrame))
            {
                var textView = VsShellUtilities.GetTextView(windowFrame);
                if (textView.GetBuffer(out var vsTextBuffer) == VSConstants.S_OK)
                {
                    var textBuffer = _textEditorAdaptersFactoryService.GetDocumentBuffer(vsTextBuffer);
                    UpdateTextBufferContentType(textBuffer, window.Document.Name);
                }
            }
        }

        public async Task ChangeRadeonExtensionsAsync(IEnumerable<string> asm1Extensions, IEnumerable<string> asm2Extensions)
        {
            _asm1Extensions = asm1Extensions;
            _asm2Extensions = asm2Extensions;
            try
            {
                DeleteExtensions(Asm1ContentType);
                DeleteExtensions(Asm2ContentType);

                ChangeExtensions(Asm1ContentType, asm1Extensions);
                ChangeExtensions(Asm2ContentType, asm2Extensions);

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

        private void DeleteExtensions(IContentType contentType)
        {
            foreach (var ext in _fileExtensionRegistryService.GetExtensionsForContentType(contentType))
                _fileExtensionRegistryService.RemoveFileExtension(ext);
        }

        private void ChangeExtensions(IContentType contentType, IEnumerable<string> extensions)
        {
            foreach (var ext in extensions)
                _fileExtensionRegistryService.AddFileExtension(ext, contentType);
        }

        private void UpdateTextBufferContentType(ITextBuffer textBuffer, string path)
        {
            if (textBuffer == null || textBuffer.ContentType == Asm1ContentType || textBuffer.ContentType == Asm2ContentType || textBuffer.ContentType == AsmDocContentType)
                return;

            var contentType = DetermineContentType(path);
            if (contentType == null) 
                return;

            UpdateTextBufferContentType(textBuffer, contentType);
        }

        private void UpdateTextBufferContentType(ITextBuffer textBuffer, IContentType contentType)
        {
            if (textBuffer == null || contentType == null)
                return;

            textBuffer.ChangeContentType(contentType, null);
        }
    }
}
