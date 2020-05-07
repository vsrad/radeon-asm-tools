using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    public class BaseToken : IBaseToken
    {
        private ITextDocument _textDocument;

        public string TokenName { get; }

        public int LineNumber => Line.LineNumber + 1;

        public ITextSnapshotLine Line { get; }

        public SnapshotSpan SymbolSpan { get; }

        public TokenType TokenType { get; }

        public string FilePath { get; }

        public BaseToken(SnapshotSpan symbolSpan, TokenType tokenType)
        {
            this.SymbolSpan = symbolSpan;
            this.Line = symbolSpan.Start.GetContainingLine();
            var result = symbolSpan.Snapshot.TextBuffer.Properties.TryGetProperty<ITextDocument>(
                  typeof(ITextDocument), out _textDocument);
            if (result == true)
                this.FilePath = _textDocument.FilePath;
            this.TokenType = tokenType;
            this.TokenName = symbolSpan.GetText();
        }

        public override int GetHashCode() =>
            TokenName.GetHashCode() ^ TokenType.GetHashCode();

        public override bool Equals(object obj)
        {
            if (base.Equals(obj) || ReferenceEquals(obj, this))
                return true;
            if (obj as BaseToken == null)
                return false;

            return ((BaseToken)obj).TokenName == TokenName
                && ((BaseToken)obj).TokenType == TokenType;
        }
    }
}
