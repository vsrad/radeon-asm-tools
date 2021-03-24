using System.Collections.Generic;
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
        private readonly Dictionary<IDocument, AnalysisClassifier> _analysisClassifiers;
        private readonly IDocumentFactory _documentFactory;

        [ImportingConstructor]
        public AnalysisClassifierProvider(
            IClassificationTypeRegistryService classificationTypeRegistryService,
            IDocumentFactory documentFactory,
            ThemeColorManager classificationColorManager)
        {
            AnalysisClassifier.InitializeClassifierDictionary(classificationTypeRegistryService);
            _analysisClassifiers = new Dictionary<IDocument, AnalysisClassifier>();
            _documentFactory = documentFactory;

            _documentFactory.DocumentDisposed += DocumentDisposed;
            Microsoft.VisualStudio.PlatformUI.VSColorTheme.ThemeChanged += (e) => classificationColorManager.UpdateColors();
        }

        private void DocumentDisposed(IDocument document)
        {
            if (!_analysisClassifiers.TryGetValue(document, out var analysis)) return;
            analysis.OnDestroy();
            _analysisClassifiers.Remove(document);
        }

        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            var document = _documentFactory.GetOrCreateDocument(textBuffer);
            if (!_analysisClassifiers.TryGetValue(document, out var analysisClassifier))
            {
                analysisClassifier = new AnalysisClassifier(document.DocumentAnalysis);
                _analysisClassifiers.Add(document, analysisClassifier);
            }
            
            return analysisClassifier;
        }
    }

    [Export(typeof(ITaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(ClassificationTag))]
    internal class TokenizerClassifierProvider : ITaggerProvider
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly Dictionary<IDocument, TokenizerClassifier> _tokenizerClassifiers;

        [ImportingConstructor]
        public TokenizerClassifierProvider(IStandardClassificationService classificationService, IDocumentFactory documentFactory)
        {
            TokenizerClassifier.InitializeClassifierDictionary(classificationService);
            _tokenizerClassifiers = new Dictionary<IDocument, TokenizerClassifier>();
            _documentFactory = documentFactory;

            _documentFactory.DocumentDisposed += DocumentDisposed;
        }

        private void DocumentDisposed(IDocument document)
        {
            if (!_tokenizerClassifiers.TryGetValue(document, out var tokenizer)) return;
            tokenizer.OnDestroy();
            _tokenizerClassifiers.Remove(document);
        }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            var document = _documentFactory.GetOrCreateDocument(buffer);
            if (!_tokenizerClassifiers.TryGetValue(document, out var tokenizerClassifier))
            {
                tokenizerClassifier = new TokenizerClassifier(document.DocumentTokenizer);
                _tokenizerClassifiers.Add(document, tokenizerClassifier);
            }
            
            return tokenizerClassifier as ITagger<T>;
        }
    }
}