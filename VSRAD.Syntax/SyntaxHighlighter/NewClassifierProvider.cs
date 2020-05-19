using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Syntax.Parser;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    public enum RadAsmTokenTypes
    {
        COMMENT,
        STRING,
        NUMBER,
        OP,
        STRUCTURAL,
        WHITESPACE,
        KEYWORD,
        UNKNOWN,
    }

    internal static class Types
    {
        private static readonly Dictionary<int, RadAsmTokenTypes> _tt = new Dictionary<int, RadAsmTokenTypes>()
        {
            { RadAsmLexer.EQ, RadAsmTokenTypes.OP },
            { RadAsmLexer.LT, RadAsmTokenTypes.OP },
            { RadAsmLexer.LE, RadAsmTokenTypes.OP },
            { RadAsmLexer.EQEQ, RadAsmTokenTypes.OP },
            { RadAsmLexer.NE, RadAsmTokenTypes.OP },
            { RadAsmLexer.GE, RadAsmTokenTypes.OP },
            { RadAsmLexer.GT, RadAsmTokenTypes.OP },
            { RadAsmLexer.ANDAND, RadAsmTokenTypes.OP },
            { RadAsmLexer.OROR, RadAsmTokenTypes.OP },
            { RadAsmLexer.NOT, RadAsmTokenTypes.OP },
            { RadAsmLexer.TILDE, RadAsmTokenTypes.OP },
            { RadAsmLexer.PLUS, RadAsmTokenTypes.OP },
            { RadAsmLexer.MINUS, RadAsmTokenTypes.OP },
            { RadAsmLexer.STAR, RadAsmTokenTypes.OP },
            { RadAsmLexer.SLASH, RadAsmTokenTypes.OP },
            { RadAsmLexer.PERCENT, RadAsmTokenTypes.OP },
            { RadAsmLexer.CARET, RadAsmTokenTypes.OP },
            { RadAsmLexer.AND, RadAsmTokenTypes.OP },
            { RadAsmLexer.OR, RadAsmTokenTypes.OP },
            { RadAsmLexer.SHL, RadAsmTokenTypes.OP },
            { RadAsmLexer.SHR, RadAsmTokenTypes.OP },
            { RadAsmLexer.BINOP, RadAsmTokenTypes.OP },

            { RadAsmLexer.TEXT, RadAsmTokenTypes.KEYWORD },
            { RadAsmLexer.SET, RadAsmTokenTypes.KEYWORD },
            { RadAsmLexer.MACRO, RadAsmTokenTypes.KEYWORD },
            { RadAsmLexer.ENDM, RadAsmTokenTypes.KEYWORD },
            { RadAsmLexer.IF, RadAsmTokenTypes.KEYWORD },
            { RadAsmLexer.IFDEF, RadAsmTokenTypes.KEYWORD },
            { RadAsmLexer.ENDIF, RadAsmTokenTypes.KEYWORD },

            { RadAsmLexer.COMMA, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.SEMI, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.COLON, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.LPAREN, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.RPAREN, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.LBRACKET, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.RBRACKET, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.LBRACE, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.RBRACE, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.POUND, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.DOLLAR, RadAsmTokenTypes.STRUCTURAL },
            { RadAsmLexer.UNDERSCORE, RadAsmTokenTypes.STRUCTURAL },

            { RadAsmLexer.CONSTANT, RadAsmTokenTypes.NUMBER },
            { RadAsmLexer.STRING_LITERAL, RadAsmTokenTypes.STRING },

            { RadAsmLexer.WHITESPACE, RadAsmTokenTypes.WHITESPACE },
            { RadAsmLexer.BLOCK_COMMENT, RadAsmTokenTypes.COMMENT },
            { RadAsmLexer.LINE_COMMENT, RadAsmTokenTypes.COMMENT },
            { RadAsmLexer.UNKNOWN, RadAsmTokenTypes.UNKNOWN },
        };

        public static RadAsmTokenTypes LexerTokenToRadAsmToken(int type) =>
            _tt[type];
    }

    [Export(typeof(ITaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(ClassificationTag))]
    internal class NewClassifierProvider : ITaggerProvider
    {
        private readonly IRadAsmLexer _lexer;
        private readonly IStandardClassificationService _classificationService;

        [ImportingConstructor]
        public NewClassifierProvider(IRadAsmLexer lexer, IStandardClassificationService classificationService)
        {
            _lexer = lexer;
            _classificationService = classificationService;
        }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag =>
            buffer.Properties.GetOrCreateSingletonProperty(() => new NewClassifier(buffer, _lexer, _classificationService)) as ITagger<T>;
    }

    internal class NewClassifier : ITagger<ClassificationTag>
    {
        static Dictionary<RadAsmTokenTypes, IClassificationType> _tokenTypes;
        private readonly ITextBuffer _buffer;
        private readonly DocumentAnalysis _documentAnalysis;

        public NewClassifier(ITextBuffer buffer, IRadAsmLexer lexer, IStandardClassificationService standardClassificationService)
        {
            _buffer = buffer;
            _documentAnalysis = DocumentAnalysis.CreateAndRegister(lexer, buffer);

            _documentAnalysis.TokensChanged += TokensChanged;
            InitializeClassifierDictionary(standardClassificationService);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private void TokensChanged(IList<TrackingToken> trackingTokens)
        {
            if (trackingTokens.Count == 0)
                return;

            var snapshot = _documentAnalysis.CurrentSnapshot;
            var start = trackingTokens.First().GetStart(snapshot);
            var end = trackingTokens.Last().GetEnd(snapshot);

            TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(start, end - start))));
        }

        void InitializeClassifierDictionary(IStandardClassificationService typeService)
        {
            if (_tokenTypes != null)
                return;

            _tokenTypes = new Dictionary<RadAsmTokenTypes, IClassificationType>()
            {
                { RadAsmTokenTypes.COMMENT, typeService.Comment },
                { RadAsmTokenTypes.NUMBER, typeService.NumberLiteral },
                { RadAsmTokenTypes.OP, typeService.Operator },
                { RadAsmTokenTypes.STRING, typeService.StringLiteral },
                { RadAsmTokenTypes.STRUCTURAL, typeService.FormalLanguage },
                { RadAsmTokenTypes.WHITESPACE, typeService.WhiteSpace },
                { RadAsmTokenTypes.KEYWORD, typeService.Keyword },
                { RadAsmTokenTypes.UNKNOWN, typeService.StringLiteral },
        };
        }

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_documentAnalysis.CurrentSnapshot.Version.VersionNumber != _buffer.CurrentSnapshot.Version.VersionNumber)
                yield break;

            foreach (var span in spans)
            {
                foreach (var token in _documentAnalysis.GetTokens(span))
                {
                    if (token.IsEmpty)
                        continue;

                    var tag = new ClassificationTag(_tokenTypes[Types.LexerTokenToRadAsmToken(token.Type)]);
                    yield return new TagSpan<ClassificationTag>(new SnapshotSpan(_documentAnalysis.CurrentSnapshot, token.GetSpan(_documentAnalysis.CurrentSnapshot)), tag);
                }
            }

        }
    }
}
