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
    internal class AnalysisClassifier : IClassifier, ISyntaxDisposable
    {
        private static Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;
        private readonly IDocumentAnalysis _documentAnalysis;
        private IAnalysisResult _analysisResult;

        public AnalysisClassifier(IDocumentAnalysis documentAnalysis)
        {
            _analysisResult = documentAnalysis.CurrentResult;
            _documentAnalysis = documentAnalysis;
            _documentAnalysis.AnalysisUpdated += AnalysisUpdated;
        }

#pragma warning disable CS0067 // disable "The event is never used". It's required by IClassifier
        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore CS0067

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var classificationSpans = new List<ClassificationSpan>();
            var analysisResult = _analysisResult;
            if (analysisResult == null || analysisResult.Snapshot != span.Snapshot) return classificationSpans;

            var block = analysisResult.GetBlock(span.Start);
            if (block.Type == BlockType.Comment) return classificationSpans;

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

        public void OnDispose()
        {
            _documentAnalysis.AnalysisUpdated -= AnalysisUpdated;
        }

        public static void InitializeClassifierDictionary(IClassificationTypeRegistryService registryService)
        {
            if (_tokenClassification != null)
                return;

            _tokenClassification = new Dictionary<RadAsmTokenType, IClassificationType>()
            {
                { RadAsmTokenType.Instruction, registryService.GetClassificationType(RadAsmTokenType.Instruction.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionName, registryService.GetClassificationType(RadAsmTokenType.FunctionName.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionReference, registryService.GetClassificationType(RadAsmTokenType.FunctionReference.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionParameter, registryService.GetClassificationType(RadAsmTokenType.FunctionParameter.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionParameterReference, registryService.GetClassificationType(RadAsmTokenType.FunctionParameterReference.GetClassificationTypeName()) },
                { RadAsmTokenType.Label, registryService.GetClassificationType(RadAsmTokenType.Label.GetClassificationTypeName()) },
                { RadAsmTokenType.LabelReference, registryService.GetClassificationType(RadAsmTokenType.LabelReference.GetClassificationTypeName()) },
            };
        }

        private void AnalysisUpdated(IAnalysisResult analysisResult, RescanReason reason, CancellationToken ct)
        {
            _analysisResult = analysisResult;

            if (reason != RescanReason.ContentChanged)
                ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(new SnapshotSpan(analysisResult.Snapshot, 0, analysisResult.Snapshot.Length)));
        }
    }

    internal class TokenizerClassifier : ITagger<ClassificationTag>, ISyntaxDisposable
    {
        private static Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;
        private readonly IDocumentTokenizer _tokenizer;
        private ITokenizerResult _currentResult;

        public TokenizerClassifier(IDocumentTokenizer tokenizer)
        {
            _tokenizer = tokenizer;
            _tokenizer.TokenizerUpdated += TokenizerUpdated;
            TokenizerUpdated(_tokenizer.CurrentResult, CancellationToken.None);
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

        public void OnDispose()
        {
            _tokenizer.TokenizerUpdated -= TokenizerUpdated;
        }

        public static void InitializeClassifierDictionary(IStandardClassificationService typeService)
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

        private void TokenizerUpdated(ITokenizerResult result, CancellationToken ct)
        {
            var tokens = result.UpdatedTokens;
            if (!tokens.Any())
                return;

            _currentResult = result;
            var start = tokens.First().GetStart(result.Snapshot);
            var end = tokens.Last().GetEnd(result.Snapshot);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(result.Snapshot, new Span(start, end - start))));
        }
    }
}
