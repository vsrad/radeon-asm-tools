using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    [Export(typeof(IClassifierProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    internal class AnalysisClassifierProvider : DisposableProvider<IDocument, AnalysisClassifier>, IClassifierProvider
    {
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public AnalysisClassifierProvider(
            IClassificationTypeRegistryService classificationTypeRegistryService,
            IDocumentFactory documentFactory,
            ThemeColorManager classificationColorManager)
        {
            AnalysisClassifier.InitializeClassifierDictionary(classificationTypeRegistryService);
            _documentFactory = documentFactory;

            _documentFactory.DocumentDisposed += DisposeRequest;
            Microsoft.VisualStudio.PlatformUI.VSColorTheme.ThemeChanged += (e) => classificationColorManager.UpdateColors();
        }

        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            var document = _documentFactory.GetOrCreateDocument(textBuffer);
            if (document == null) return null;

            return GetValue(document, () => new AnalysisClassifier(document.DocumentAnalysis));
        }
    }

    [Export(typeof(ITaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(ClassificationTag))]
    internal class TokenizerClassifierProvider : DisposableProvider<IDocument, TokenizerClassifier>, ITaggerProvider
    {
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public TokenizerClassifierProvider(IStandardClassificationService classificationService, IDocumentFactory documentFactory)
        {
            TokenizerClassifier.InitializeClassifierDictionary(classificationService);
            _documentFactory = documentFactory;
            _documentFactory.DocumentDisposed += DisposeRequest;
        }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            var document = _documentFactory.GetOrCreateDocument(buffer);
            if (document == null) return null;

            return GetValue(document, () => new TokenizerClassifier(document.DocumentTokenizer)) as ITagger<T>;
        }
    }
}