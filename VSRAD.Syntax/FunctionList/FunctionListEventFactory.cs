using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.FunctionList
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class FunctionListEventFactory : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;

        [ImportingConstructor]
        public FunctionListEventFactory(IVsEditorAdaptersFactoryService adaptersFactoryService, 
            DocumentAnalysisProvoder documentAnalysisProvoder)
        {
            _adaptersFactoryService = adaptersFactoryService;
            _documentAnalysisProvoder = documentAnalysisProvoder;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var view = _adaptersFactoryService.GetWpfTextView(textViewAdapter);

            if (view != null)
            {
                var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(view.TextBuffer);

                documentAnalysis.ParserUpdated += FunctionList.TryUpdateFunctionList;
                view.Caret.PositionChanged += (obj, args) => FunctionList.TryHighlightCurrentFunction(args.TextView);

                FunctionList.TryUpdateFunctionList(documentAnalysis.CurrentSnapshot, documentAnalysis.LastParserResult);
            }
        }
    }
}
