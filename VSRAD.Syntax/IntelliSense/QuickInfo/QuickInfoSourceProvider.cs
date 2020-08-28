using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(QuickInfoSourceProvider))]
    [Order]
    internal class QuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly INavigationTokenService _navigationService;
        private readonly IIntellisenseDescriptionBuilder _descriptionBuilder;

        [ImportingConstructor]
        public QuickInfoSourceProvider(IDocumentFactory documentFactory,
            INavigationTokenService navigationService,
            IIntellisenseDescriptionBuilder descriptionBuilder)
        {
            _documentFactory = documentFactory;
            _navigationService = navigationService;
            _descriptionBuilder = descriptionBuilder;
        }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(textBuffer);
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new QuickInfoSource(textBuffer, documentAnalysis, _navigationService, _descriptionBuilder));
        }
    }
}
