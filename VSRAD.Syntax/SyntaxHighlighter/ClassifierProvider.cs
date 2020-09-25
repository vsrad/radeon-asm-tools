using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    [Export(typeof(IClassifierProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    internal class AnalysisClassifierProvider : IClassifierProvider
    {
        private readonly IClassificationTypeRegistryService _classificationTypeRegistryService;
        private readonly IDocumentFactory _documentFactory;
        private readonly RadeonServiceProvider _serviceProvider;

        [ImportingConstructor]
        public AnalysisClassifierProvider(
            IClassificationTypeRegistryService classificationTypeRegistryService,
            IDocumentFactory documentFactory,
            ThemeColorManager classificationColorManager)
        {
            _classificationTypeRegistryService = classificationTypeRegistryService;
            _documentFactory = documentFactory;

            Microsoft.VisualStudio.PlatformUI.VSColorTheme.ThemeChanged += (e) => classificationColorManager.UpdateColors();
        }

        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            var document = _documentFactory.GetOrCreateDocument(textBuffer);
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new AnalysisClassifier(document.DocumentAnalysis, _classificationTypeRegistryService));
        }
    }

    [Export(typeof(ITaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(ClassificationTag))]
    internal class TokenizerClassifierProvider : ITaggerProvider
    {
        private readonly IStandardClassificationService _classificationService;
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public TokenizerClassifierProvider(IStandardClassificationService classificationService, IDocumentFactory documentFactory)
        {
            _classificationService = classificationService;
            _documentFactory = documentFactory;
        }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            var document = _documentFactory.GetOrCreateDocument(buffer);
            return buffer.Properties.GetOrCreateSingletonProperty(() => new TokenizerClassifier(document.DocumentTokenizer, _classificationService)) as ITagger<T>;
        }
    }
}