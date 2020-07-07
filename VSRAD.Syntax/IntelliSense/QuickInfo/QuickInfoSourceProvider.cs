using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(QuickInfoSourceProvider))]
    [Order]
    internal class QuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        private readonly INavigationTokenService _navigationService;

        [ImportingConstructor]
        public QuickInfoSourceProvider(DocumentAnalysisProvoder documentAnalysisProvoder,
            INavigationTokenService navigationService)
        {
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _navigationService = navigationService;
        }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(textBuffer);
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new QuickInfoSource(textBuffer, documentAnalysis, _navigationService));
        }
    }
}
