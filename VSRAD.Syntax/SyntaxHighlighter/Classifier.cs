using VSRAD.Syntax.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Language.StandardClassification;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    internal class AnalysisClassifier : DocumentObserver, IClassifier
    {
        private Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;
        private IAnalysisResult _analysisResult;
        private readonly IDocumentAnalysis _documentAnalysis;

        public AnalysisClassifier(IDocument document, IClassificationTypeRegistryService typeRegistryService)
            : base(document)
        {
            _documentAnalysis = document.DocumentAnalysis;
            _analysisResult = _documentAnalysis.CurrentResult;

            _documentAnalysis.AnalysisUpdated += AnalysisUpdated;

            InitializeClassifierDictonary(typeRegistryService);
        }

#pragma warning disable CS0067 // disable "The event is never used". It's required by IClassifier
        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore CS0067

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var classificationSpans = new List<ClassificationSpan>();
            var analysisResult = _analysisResult;
            if (analysisResult == null || analysisResult.Snapshot != span.Snapshot) return classificationSpans;

            var point = span.End - 1; // span is right exclusive
            var block = analysisResult.GetBlock(point);
            if (block.Type == BlockType.Comment) return classificationSpans;
            if (block.Type == BlockType.Function)
            {
                var funcBlock = (FunctionBlock)block;
                var funcToken = funcBlock.Name;
                var funcClassificationSpan = new ClassificationSpan(funcToken.Span, _tokenClassification[funcToken.Type]);
                classificationSpans.Add(funcClassificationSpan);
            }

            foreach (var scopeToken in block.Tokens)
            {
                switch (scopeToken.Type)
                {
                    case RadAsmTokenType.GlobalVariable:
                    case RadAsmTokenType.GlobalVariableReference:
                    case RadAsmTokenType.LocalVariable:
                    case RadAsmTokenType.LocalVariableReference:
                        continue;
                }

                classificationSpans.Add(new ClassificationSpan(scopeToken.Span, _tokenClassification[scopeToken.Type]));
            }

            return classificationSpans;
        }

        private void InitializeClassifierDictonary(IClassificationTypeRegistryService registryService)
        {
            _tokenClassification = new Dictionary<RadAsmTokenType, IClassificationType>()
            {
                { RadAsmTokenType.Instruction, registryService.GetClassificationType(RadAsmTokenType.Instruction.GetClassificationTypeName()) },
                { RadAsmTokenType.BuiltinFunction, registryService.GetClassificationType(RadAsmTokenType.BuiltinFunction.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionName, registryService.GetClassificationType(RadAsmTokenType.FunctionName.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionReference, registryService.GetClassificationType(RadAsmTokenType.FunctionReference.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionParameter, registryService.GetClassificationType(RadAsmTokenType.FunctionParameter.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionParameterReference, registryService.GetClassificationType(RadAsmTokenType.FunctionParameterReference.GetClassificationTypeName()) },
                { RadAsmTokenType.Label, registryService.GetClassificationType(RadAsmTokenType.Label.GetClassificationTypeName()) },
                { RadAsmTokenType.LabelReference, registryService.GetClassificationType(RadAsmTokenType.LabelReference.GetClassificationTypeName()) },
                { RadAsmTokenType.Keyword, registryService.GetClassificationType(RadAsmTokenType.Keyword.GetClassificationTypeName()) },
            };
        }

        private void AnalysisUpdated(IAnalysisResult analysisResult, RescanReason reason, CancellationToken ct)
        {
            _analysisResult = analysisResult;

            if (reason != RescanReason.ContentChanged)
                ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(new SnapshotSpan(analysisResult.Snapshot, 0, analysisResult.Snapshot.Length)));
        }

        protected override void OnClosingDocument(IDocument document)
        {
            _documentAnalysis.AnalysisUpdated -= AnalysisUpdated;
        }
    }

    internal class TokenizerClassifier : DocumentObserver, ITagger<ClassificationTag>
    {
        private static Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;
        private readonly IDocumentTokenizer _tokenizer;
        private ITokenizerResult _currentResult;

        public TokenizerClassifier(IDocument document, IStandardClassificationService standardClassificationService)
            : base(document)
        {
            _tokenizer = document.DocumentTokenizer;
            _tokenizer.TokenizerUpdated += TokenizerUpdated;

            InitializeClassifierDictionary(standardClassificationService);
            TokenizerUpdated(_tokenizer.CurrentResult, RescanReason.ContentChanged, CancellationToken.None);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var result = _currentResult;
            foreach (var span in spans)
            {
                foreach (var token in result.GetTokens(span))
                {
                    if (token.IsEmpty || _tokenizer.GetTokenType(token.Type) == RadAsmTokenType.Identifier)
                        continue;

                    var tag = new ClassificationTag(_tokenClassification[_tokenizer.GetTokenType(token.Type)]);
                    yield return new TagSpan<ClassificationTag>(new SnapshotSpan(result.Snapshot, token.GetSpan(result.Snapshot)), tag);
                }
            }
        }

        private void InitializeClassifierDictionary(IStandardClassificationService typeService)
        {
            if (_tokenClassification != null)
                return;

            _tokenClassification = new Dictionary<RadAsmTokenType, IClassificationType>()
            {
                { RadAsmTokenType.Comment, typeService.Comment },
                { RadAsmTokenType.Number, typeService.NumberLiteral },
                { RadAsmTokenType.Identifier, typeService.FormalLanguage },
                { RadAsmTokenType.Operation, typeService.Operator },
                { RadAsmTokenType.String, typeService.StringLiteral },
                { RadAsmTokenType.Structural, typeService.FormalLanguage },
                { RadAsmTokenType.Comma, typeService.FormalLanguage },
                { RadAsmTokenType.Semi, typeService.FormalLanguage },
                { RadAsmTokenType.Colon, typeService.FormalLanguage },
                { RadAsmTokenType.Lparen, typeService.FormalLanguage },
                { RadAsmTokenType.Rparen, typeService.FormalLanguage },
                { RadAsmTokenType.LsquareBracket, typeService.FormalLanguage },
                { RadAsmTokenType.RsquareBracket, typeService.FormalLanguage },
                { RadAsmTokenType.LcurveBracket, typeService.FormalLanguage },
                { RadAsmTokenType.RcurveBracket, typeService.FormalLanguage },
                { RadAsmTokenType.Whitespace, typeService.WhiteSpace },
                { RadAsmTokenType.Keyword, typeService.Keyword },
                { RadAsmTokenType.Preprocessor, typeService.PreprocessorKeyword },
                { RadAsmTokenType.Unknown, typeService.Other },
            };
        }

        private void TokenizerUpdated(ITokenizerResult result, RescanReason rs, CancellationToken ct)
        {
            var tokens = result.UpdatedTokens;
            if (!tokens.Any())
                return;

            _currentResult = result;
            var start = tokens.First().GetStart(result.Snapshot);
            var end = tokens.Last().GetEnd(result.Snapshot);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(result.Snapshot, new Span(start, end - start))));
        }

        protected override void OnClosingDocument(IDocument document)
        {
            _tokenizer.TokenizerUpdated -= TokenizerUpdated;
        }
    }
}
