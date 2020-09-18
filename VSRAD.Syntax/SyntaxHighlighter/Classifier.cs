using VSRAD.Syntax.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Language.StandardClassification;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Core.Blocks;
using System.Threading;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    internal class AnalysisClassifier : ITagger<ClassificationTag>
    {
        private readonly IDocumentAnalysis _documentAnalysis;
        private Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;
        private IAnalysisResult _analysisResult;

        public AnalysisClassifier(IDocumentAnalysis documentAnalysis,
            IClassificationTypeRegistryService classificationTypeRegistryService,
            IStandardClassificationService standardClassificationService)
        {
            _documentAnalysis = documentAnalysis;
            _documentAnalysis.AnalysisUpdated += AnalysisUpdated;

            InitializeClassifierDictonary(standardClassificationService, classificationTypeRegistryService);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var analysisResult = _analysisResult;
            if (analysisResult == null) yield break;

            foreach (var block in analysisResult.Scopes)
            {
                if (block.Type == BlockType.Comment) continue;

                foreach (var scopeToken in block.Tokens)
                    yield return GetTag(scopeToken);
            }
        }

        private TagSpan<ClassificationTag> GetTag(AnalysisToken token)
        {
            // iteration of the tagger can be invoked by VSStd2KCmdID.BACKSPACE of default IOleCommandTarget,
            // while the parser may not have been executed yet and may occur ArgumentOutOfRangeException
            var tag = new ClassificationTag(_tokenClassification[token.Type]);

            return new TagSpan<ClassificationTag>(token.Span, tag);
        }

        private void InitializeClassifierDictonary(IStandardClassificationService typeService, IClassificationTypeRegistryService registryService)
        {
            _tokenClassification = new Dictionary<RadAsmTokenType, IClassificationType>()
            {
                { RadAsmTokenType.Instruction, registryService.GetClassificationType(RadAsmTokenType.Instruction.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionName, registryService.GetClassificationType(RadAsmTokenType.FunctionName.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionReference, registryService.GetClassificationType(RadAsmTokenType.FunctionReference.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionParameter, registryService.GetClassificationType(RadAsmTokenType.FunctionParameter.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionParameterReference, registryService.GetClassificationType(RadAsmTokenType.FunctionParameterReference.GetClassificationTypeName()) },
                { RadAsmTokenType.Label, registryService.GetClassificationType(RadAsmTokenType.Label.GetClassificationTypeName()) },
                { RadAsmTokenType.LabelReference, registryService.GetClassificationType(RadAsmTokenType.LabelReference.GetClassificationTypeName()) },
                { RadAsmTokenType.GlobalVariable, typeService.FormalLanguage },
                { RadAsmTokenType.GlobalVariableReference, typeService.FormalLanguage },
                { RadAsmTokenType.LocalVariable, typeService.FormalLanguage },
                //{ RadAsmTokenType.LocalVariableReference, typeService.FormalLanguage },
            };
        }

        private void AnalysisUpdated(IAnalysisResult analysisResult, CancellationToken _)
        {
            _analysisResult = analysisResult;

            var span = new SnapshotSpan(_analysisResult.Snapshot, new Span(0, _analysisResult.Snapshot.Length));
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }
    }

    internal class TokenizerClassifier : ITagger<ClassificationTag>
    {
        private static Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;
        private readonly IDocumentTokenizer _tokenizer;
        private TokenizerResult _currentResult;

        public TokenizerClassifier(IDocumentTokenizer tokenizer, IStandardClassificationService standardClassificationService)
        {
            _tokenizer = tokenizer;
            _tokenizer.TokenizerUpdated += (result, ct) => TokenizerUpdated(result);

            InitializeClassifierDictionary(standardClassificationService);
            TokenizerUpdated(_tokenizer.CurrentResult);
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

        private void TokenizerUpdated(TokenizerResult result)
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
