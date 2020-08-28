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

namespace VSRAD.Syntax.SyntaxHighlighter
{
    //internal class AnalysisClassifier : ITagger<ClassificationTag>
    //{
    //    private readonly ITextBuffer _buffer;
    //    private readonly DocumentAnalysis _documentAnalysis;
    //    private Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;

    //    public AnalysisClassifier(ITextBuffer buffer,
    //        DocumentAnalysis documentAnalysis,
    //        IClassificationTypeRegistryService classificationTypeRegistryService,
    //        IStandardClassificationService standardClassificationService)
    //    {
    //        _buffer = buffer;
    //        _documentAnalysis = documentAnalysis;
    //        documentAnalysis.ParserUpdated += ParserUpdated;

    //        InitializeClassifierDictonary(standardClassificationService, classificationTypeRegistryService);
    //        ParserUpdated(documentAnalysis.CurrentSnapshot, documentAnalysis.LastParserResult);
    //    }

    //    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    //    public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    //    {
    //        var snapshot = _documentAnalysis.CurrentSnapshot;
    //        if (snapshot != _buffer.CurrentSnapshot)
    //            yield break;

    //        foreach (var block in _documentAnalysis.LastParserResult)
    //        {
    //            if (block.Type == BlockType.Comment)
    //                continue;

    //            if (block.Type == BlockType.Function)
    //            {
    //                var name = ((FunctionBlock)block).Name;
    //                yield return GetTag(snapshot, name);
    //            }

    //            foreach (var scopeToken in block.Tokens)
    //            {
    //                yield return GetTag(snapshot, scopeToken);
    //            }
    //        }
    //    }

    //    private TagSpan<ClassificationTag> GetTag(ITextSnapshot snapshot, AnalysisToken token)
    //    {
    //        // iteration of the tagger can be invoked by VSStd2KCmdID.BACKSPACE of default IOleCommandTarget,
    //        // while the parser may not have been executed yet and may occur ArgumentOutOfRangeException
    //        var span = token.TrackingToken.GetSpan(snapshot);
    //        if (span.End > snapshot.Length)
    //            return null;

    //        var snapshotSpan = new SnapshotSpan(snapshot, span);
    //        var tag = new ClassificationTag(_tokenClassification[token.Type]);

    //        return new TagSpan<ClassificationTag>(snapshotSpan, tag);
    //    }

    //    private void InitializeClassifierDictonary(IStandardClassificationService typeService, IClassificationTypeRegistryService registryService)
    //    {
    //        if (_tokenClassification != null)
    //            return;

    //        _tokenClassification = new Dictionary<RadAsmTokenType, IClassificationType>()
    //        {
    //            { RadAsmTokenType.Instruction, registryService.GetClassificationType(RadAsmTokenType.Instruction.GetClassificationTypeName()) },
    //            { RadAsmTokenType.FunctionName, registryService.GetClassificationType(RadAsmTokenType.FunctionName.GetClassificationTypeName()) },
    //            { RadAsmTokenType.FunctionReference, registryService.GetClassificationType(RadAsmTokenType.FunctionReference.GetClassificationTypeName()) },
    //            { RadAsmTokenType.FunctionParameter, registryService.GetClassificationType(RadAsmTokenType.FunctionParameter.GetClassificationTypeName()) },
    //            { RadAsmTokenType.FunctionParameterReference, registryService.GetClassificationType(RadAsmTokenType.FunctionParameterReference.GetClassificationTypeName()) },
    //            { RadAsmTokenType.Label, registryService.GetClassificationType(RadAsmTokenType.Label.GetClassificationTypeName()) },
    //            { RadAsmTokenType.LabelReference, registryService.GetClassificationType(RadAsmTokenType.LabelReference.GetClassificationTypeName()) },
    //            { RadAsmTokenType.GlobalVariable, typeService.FormalLanguage },
    //            { RadAsmTokenType.GlobalVariableReference, typeService.FormalLanguage },
    //            { RadAsmTokenType.LocalVariable, typeService.FormalLanguage },
    //        };
    //    }

    //    private void ParserUpdated(ITextSnapshot version, IReadOnlyList<IBlock> blocks)
    //    {
    //        if (blocks.Count == 0)
    //            return;

    //        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(version, new Span(0, version.Length))));
    //    }
    //}

    internal class TokenizerClassifier : ITagger<ClassificationTag>
    {
        private static Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;
        private readonly IDocumentTokenizer _tokenizer;
        private ITextSnapshot currentSnapshot;
        private IEnumerable<TrackingToken> currentTokens;

        public TokenizerClassifier(IDocumentTokenizer tokenizer, IStandardClassificationService standardClassificationService)
        {
            _tokenizer = tokenizer;
            _tokenizer.TokenizerUpdated += TokenizerUpdated;
            InitializeClassifierDictionary(standardClassificationService);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var snapshot = currentSnapshot;
            var tokens = currentTokens;

            foreach (var token in tokens)
            {
                if (token.IsEmpty || _tokenizer.GetTokenType(token.Type) == RadAsmTokenType.Identifier)
                    continue;

                var tag = new ClassificationTag(_tokenClassification[_tokenizer.GetTokenType(token.Type)]);
                yield return new TagSpan<ClassificationTag>(new SnapshotSpan(snapshot, token.GetSpan(snapshot)), tag);
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

        private void TokenizerUpdated(ITextSnapshot snapshot, IEnumerable<TrackingToken> tokens)
        {
            if (!tokens.Any())
                return;

            currentSnapshot = snapshot;
            currentTokens = tokens;

            var start = tokens.First().GetStart(snapshot);
            var end = tokens.Last().GetEnd(snapshot);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(start, end - start))));
        }
    }
}
