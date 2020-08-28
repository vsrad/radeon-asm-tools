using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    [Export(typeof(ITaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(ClassificationTag))]
    internal class ClassifierProvider : ITaggerProvider
    {
        private readonly IStandardClassificationService _classificationService;
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public ClassifierProvider(IStandardClassificationService classificationService, IDocumentFactory documentFactory)
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

    //[Export(typeof(ITaggerProvider))]
    //[ContentType(Constants.RadeonAsmSyntaxContentType)]
    //[TagType(typeof(ClassificationTag))]
    //internal class AnalysisClassifierProvider : ITaggerProvider
    //{
    //    private readonly IStandardClassificationService _classificationService;
    //    private readonly IClassificationTypeRegistryService _classificationTypeRegistryService;
    //    private readonly DocumentAnalysisProvoder _documentAnalysisProvider;

    //    [ImportingConstructor]
    //    public AnalysisClassifierProvider(IStandardClassificationService classificationService,
    //        IClassificationTypeRegistryService classificationTypeRegistryService,
    //        DocumentAnalysisProvoder documentAnalysisProvoder,
    //        ThemeColorManager classificationColorManager)
    //    {
    //        _classificationService = classificationService;
    //        _classificationTypeRegistryService = classificationTypeRegistryService;
    //        _documentAnalysisProvider = documentAnalysisProvoder;
    //        Microsoft.VisualStudio.PlatformUI.VSColorTheme.ThemeChanged += (e) => classificationColorManager.UpdateColors();
    //    }

    //    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    //    {
    //        var documentAnalysis = _documentAnalysisProvider.CreateDocumentAnalysis(buffer);
    //        return buffer.Properties.GetOrCreateSingletonProperty(() => new AnalysisClassifier(buffer, documentAnalysis, _classificationTypeRegistryService, _classificationService)) as ITagger<T>;
    //    }
    //}
}