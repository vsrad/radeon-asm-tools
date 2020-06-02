using VSRAD.Syntax.Parser;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Language.StandardClassification;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.Parser.Blocks;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    internal class AnalysisClassifier : ITagger<ClassificationTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly DocumentAnalysis _documentAnalysis;
        private Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;

        public AnalysisClassifier(ITextBuffer buffer,
            DocumentAnalysis documentAnalysis,
            IClassificationTypeRegistryService classificationTypeRegistryService,
            IStandardClassificationService standardClassificationService)
        {
            _buffer = buffer;
            _documentAnalysis = documentAnalysis;
            documentAnalysis.ParserUpdated += ParserUpdated;

            InitializeClassifierDictonary(standardClassificationService, classificationTypeRegistryService);
            ParserUpdated(documentAnalysis.CurrentSnapshot, documentAnalysis.LastParserResult);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_documentAnalysis.CurrentSnapshot.Version.VersionNumber != _buffer.CurrentSnapshot.Version.VersionNumber)
                yield break;

            foreach (var block in _documentAnalysis.LastParserResult)
            {
                if (block.Type == BlockType.Comment)
                    continue;

                if (block.Type == BlockType.Function)
                {
                    var name = ((FunctionBlock)block).Name;
                    yield return GetTag(name);
                }

                foreach (var scopeToken in block.Tokens)
                {
                    yield return GetTag(scopeToken);
                }
            }
        }

        private TagSpan<ClassificationTag> GetTag(AnalysisToken token) =>
            new TagSpan<ClassificationTag>(new SnapshotSpan(_documentAnalysis.CurrentSnapshot, token.TrackingToken.GetSpan(_documentAnalysis.CurrentSnapshot)), new ClassificationTag(_tokenClassification[token.Type]));

        private void InitializeClassifierDictonary(IStandardClassificationService typeService, IClassificationTypeRegistryService registryService)
        {
            if (_tokenClassification != null)
                return;

            _tokenClassification = new Dictionary<RadAsmTokenType, IClassificationType>()
            {
                { RadAsmTokenType.Instruction, registryService.GetClassificationType(PredefinedClassificationTypeNames.Instructions) },
                { RadAsmTokenType.FunctionName, registryService.GetClassificationType(PredefinedClassificationTypeNames.Functions) },
                { RadAsmTokenType.FunctionParameter, registryService.GetClassificationType(PredefinedClassificationTypeNames.Arguments) },
                { RadAsmTokenType.FunctionParameterReference, registryService.GetClassificationType(PredefinedClassificationTypeNames.Arguments) },
                { RadAsmTokenType.Label, registryService.GetClassificationType(PredefinedClassificationTypeNames.Labels) },
                { RadAsmTokenType.GlobalVariable, typeService.FormalLanguage },
                { RadAsmTokenType.LocalVariable, typeService.FormalLanguage },
                { RadAsmTokenType.Include, typeService.StringLiteral },
            };
        }

        private void ParserUpdated(ITextSnapshot version, IReadOnlyList<IBlock> blocks)
        {
            if (blocks.Count == 0)
                return;

            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(version, new Span(0, version.Length))));
        }
    }

    internal class TokenizerClassifier : ITagger<ClassificationTag>
    {
        private static Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;
        private readonly ITextBuffer _buffer;
        private readonly DocumentAnalysis _documentAnalysis;

        public TokenizerClassifier(ITextBuffer buffer,
            DocumentAnalysis documentAnalysis,
            IStandardClassificationService standardClassificationService)
        {
            _buffer = buffer;
            _documentAnalysis = documentAnalysis;

            _documentAnalysis.TokensChanged += TokensChanged;
            InitializeClassifierDictionary(standardClassificationService);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_documentAnalysis.CurrentSnapshot.Version.VersionNumber != _buffer.CurrentSnapshot.Version.VersionNumber)
                yield break;

            foreach (var span in spans)
            {
                foreach (var token in _documentAnalysis.GetTokens(span))
                {
                    if (token.IsEmpty || token.Type == _documentAnalysis.IDENTIFIER)
                        continue;

                    var tag = new ClassificationTag(_tokenClassification[_documentAnalysis.LexerTokenToRadAsmToken(token.Type)]);
                    yield return new TagSpan<ClassificationTag>(new SnapshotSpan(_documentAnalysis.CurrentSnapshot, token.GetSpan(_documentAnalysis.CurrentSnapshot)), tag);
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
                { RadAsmTokenType.Whitespace, typeService.WhiteSpace },
                { RadAsmTokenType.Keyword, typeService.Keyword },
                { RadAsmTokenType.Preprocessor, typeService.PreprocessorKeyword },
                { RadAsmTokenType.Unknown, typeService.Other },
            };
        }

        private void TokensChanged(IList<TrackingToken> trackingTokens)
        {
            if (trackingTokens.Count == 0)
                return;

            var snapshot = _documentAnalysis.CurrentSnapshot;
            var start = trackingTokens.First().GetStart(snapshot);
            var end = trackingTokens.Last().GetEnd(snapshot);

            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(start, end - start))));
        }
    }
}
