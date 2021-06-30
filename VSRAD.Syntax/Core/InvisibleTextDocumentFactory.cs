using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace VSRAD.Syntax.Core
{
    public interface IInvisibleTextDocumentFactory
    {
        ITextDocument CreateAndLoadTextDocument(string path, IContentType contentType);
    }

    [Export(typeof(IInvisibleTextDocumentFactory))]
    internal class InvisibleTextDocumentFactory : IInvisibleTextDocumentFactory
    {
        private readonly System.IServiceProvider _serviceProvider;
        private readonly ITextDocumentFactoryService _documentFactory;
        private readonly IVsEditorAdaptersFactoryService _adapterFactory;

        [ImportingConstructor]
        public InvisibleTextDocumentFactory([Import(typeof(SVsServiceProvider))] System.IServiceProvider serviceProvider,
            IVsEditorAdaptersFactoryService adapterFactory,
            ITextDocumentFactoryService documentFactory)
        {
            _serviceProvider = serviceProvider;
            _adapterFactory = adapterFactory;
            _documentFactory = documentFactory;
        }


        public ITextDocument CreateAndLoadTextDocument(string path, IContentType contentType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Assumes.NotNullOrEmpty(path);
            Assumes.NotNull(contentType);

            var invisibleEditor = CreateInvisibleEditor(path);
            var documentData = GetDocumentData(invisibleEditor);

            var vsTextBuffer = GetDataBuffer(documentData);
            var textBuffer = GetVisualBuffer(vsTextBuffer, contentType);

            var codeWindow = CreateAssociatedCodeWindow(documentData);
            var isDocumentLoaded = _documentFactory.TryGetTextDocument(textBuffer, out var textDocument);

            Assumes.True(isDocumentLoaded, $"Invisible text document not loaded - {path}");
            return textDocument;
        }

        private IVsInvisibleEditor CreateInvisibleEditor(string filePath)
        {
            var serviceProvider = ServiceProvider.GlobalProvider;
            var invisibleEditorManager = serviceProvider.GetService(typeof(SVsInvisibleEditorManager)) as IVsInvisibleEditorManager;
            Assumes.Present(invisibleEditorManager);

            ErrorHandler.ThrowOnFailure(invisibleEditorManager.RegisterInvisibleEditor(
                filePath, 
                pProject: null, 
                dwFlags: (uint)_EDITORREGFLAGS.RIEF_ENABLECACHING, 
                pFactory: null, 
                ppEditor: out var invisibleEditor));

            return invisibleEditor;
        }

        private IVsTextLines GetDocumentData(IVsInvisibleEditor invisibleEditor)
        {
            var guidIVsTextLines = typeof(IVsTextLines).GUID;

            ErrorHandler.ThrowOnFailure(
                invisibleEditor.GetDocData(
                    fEnsureWritable: 0,
                    riid: ref guidIVsTextLines,
                    ppDocData: out var docDataPointer));

            return (IVsTextLines)Marshal.GetObjectForIUnknown(docDataPointer);
        }

        private IVsTextBuffer GetDataBuffer(IVsTextLines docData)
        {
            var vsTextBuffer = docData as IVsTextBuffer;
            var textManager = _serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
            Assumes.NotNull(vsTextBuffer);
            Assumes.Present(textManager);

            ErrorHandler.ThrowOnFailure(textManager.RegisterBuffer(vsTextBuffer));
            return vsTextBuffer;
        }

        private ITextBuffer GetVisualBuffer(IVsTextBuffer vsTextBuffer, IContentType contentType)
        {
            var textBuffer = _adapterFactory.GetDataBuffer(vsTextBuffer);
            Assumes.NotNull(textBuffer);

            textBuffer.ChangeContentType(contentType, editTag: null);
            return textBuffer;
        }

        private IVsCodeWindow CreateAssociatedCodeWindow(IVsTextLines docData)
        {
            var oleService = _serviceProvider.GetService(typeof(IServiceProvider)) as IServiceProvider;
            Assumes.Present(oleService);

            var codeWindow = _adapterFactory.CreateVsCodeWindowAdapter(oleService);
            Assumes.NotNull(codeWindow);

            ErrorHandler.ThrowOnFailure(codeWindow.SetBuffer(docData));
            return codeWindow;
        }
    }
}
