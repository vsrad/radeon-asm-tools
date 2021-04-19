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

        private readonly RadeonServiceProvider _serviceProvider;
        private readonly IVsEditorAdaptersFactoryService _textEditorAdaptersFactoryService;
        private readonly IFileExtensionRegistryService _fileExtensionRegistryService;
        private readonly DTE _dte;
        private IEnumerable<string> _asm1Extensions;
        private IEnumerable<string> _asm2Extensions;
        private readonly List<string> _asmDocExtensions;

        [ImportingConstructor]
        public ContentTypeManager(RadeonServiceProvider serviceProvider, 
            IFileExtensionRegistryService fileExtensionRegistryService, 
            IContentTypeRegistryService contentTypeRegistryService)
        {
            _serviceProvider = serviceProvider;
            _textEditorAdaptersFactoryService = serviceProvider.EditorAdaptersFactoryService;
            _fileExtensionRegistryService = fileExtensionRegistryService;
            _dte = serviceProvider.Dte;

            Asm1ContentType = contentTypeRegistryService.GetContentType(Constants.RadeonAsmSyntaxContentType);
            Asm2ContentType = contentTypeRegistryService.GetContentType(Constants.RadeonAsm2SyntaxContentType);
            AsmDocContentType = contentTypeRegistryService.GetContentType(Constants.RadeonAsmDocumentationContentType);

            var optionProvider = GeneralOptionProvider.Instance;
            _asm1Extensions = optionProvider.Asm1FileExtensions;
            _asm2Extensions = optionProvider.Asm2FileExtensions;
            _asmDocExtensions = new List<string>() { Constants.FileExtensionAsm1Doc, Constants.FileExtensionAsm2Doc };

            _dte.Events.WindowEvents.WindowActivated += OnChangeActivatedWindow;
            optionProvider.OptionsUpdated += FileExtensionChanged;
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
            var vsTextBuffer = Utils.GetWindowVisualBuffer(window, _serviceProvider.ServiceProvider);
            if (vsTextBuffer == null) return;

            var textBuffer = _textEditorAdaptersFactoryService.GetDocumentBuffer(vsTextBuffer);
            UpdateTextBufferContentType(textBuffer, window.Document.Name);
        }

        public async Task ChangeRadeonExtensionsAsync(IReadOnlyList<string> asm1Extensions, IReadOnlyList<string> asm2Extensions)
        {
            var forRemove = _asm1Extensions.Except(asm1Extensions).Concat(_asm2Extensions.Except(asm2Extensions));
            var forAddAsm1 = asm1Extensions.Except(_asm1Extensions);
            var forAddAsm2 = asm2Extensions.Except(_asm2Extensions);

            try
            {
                DeleteExtensions(forRemove);

                ChangeExtensions(Asm1ContentType, forAddAsm1);
                ChangeExtensions(Asm2ContentType, forAddAsm2);

                _asm1Extensions = asm1Extensions;
                _asm2Extensions = asm2Extensions;
            }
            catch (InvalidOperationException e)
            {
                // revert content types
                DeleteExtensions(Asm1ContentType);
                DeleteExtensions(Asm2ContentType);
                ChangeExtensions(Asm1ContentType, _asm1Extensions);
                ChangeExtensions(Asm2ContentType, _asm2Extensions);

                Error.ShowWarning(e);
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_dte.ActiveDocument != null)
                UpdateWindowContentType(_dte.ActiveDocument.ActiveWindow);
        }

        private void FileExtensionChanged(GeneralOptionProvider generalOptionProvider) =>
            ThreadHelper.JoinableTaskFactory.RunAsync(() => ChangeRadeonExtensionsAsync(generalOptionProvider.Asm1FileExtensions, generalOptionProvider.Asm2FileExtensions));

        private void DeleteExtensions(IContentType contentType)
        {
            foreach (var ext in _fileExtensionRegistryService.GetExtensionsForContentType(contentType))
                _fileExtensionRegistryService.RemoveFileExtension(ext);
        }

        private void DeleteExtensions(IEnumerable<string> extensions)
        {
            foreach (var ext in extensions)
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

        private static void UpdateTextBufferContentType(ITextBuffer textBuffer, IContentType contentType)
        {
            if (textBuffer == null || contentType == null)
                return;

            textBuffer.ChangeContentType(contentType, null);
        }
    }
}
