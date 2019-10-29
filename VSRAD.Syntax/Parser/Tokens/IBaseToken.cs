using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Tokens
{
    public interface IBaseToken
    {
        string TokenName { get; }

        int LineNumber { get; }

        ITextSnapshotLine Line { get; }

        SnapshotSpan SymbolSpan { get; }

        TokenType TokenType { get; }

        string FilePath { get; }
    }
}
