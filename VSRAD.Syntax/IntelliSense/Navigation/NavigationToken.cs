using Microsoft.VisualStudio.Text;
using System;
using System.Text;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    public interface ITokenLine
    {
        int LineStart { get; }
        int LineNumber { get; }
        string LineText { get; }
    }

    public interface INavigationToken
    {
        IAnalysisToken AnalysisToken { get; }
        RadAsmTokenType Type { get; }
        string Path { get; }
        ITokenLine Line { get; }

        void Navigate();
    }

    internal readonly struct TokenLine : ITokenLine
    {
        private readonly Lazy<ITextSnapshotLine> _lineLazy;

        public TokenLine(IAnalysisToken token)
        {
            _lineLazy = new Lazy<ITextSnapshotLine>(() => token.Span.Start.GetContainingLine());
        }

        public int LineStart => _lineLazy.Value.Start;
        public int LineNumber => _lineLazy.Value.LineNumber;
        public string LineText => _lineLazy.Value.GetText();
    }

    public class NavigationToken : INavigationToken
    {
        public IAnalysisToken AnalysisToken { get; }
        public string Path { get; }
        public ITokenLine Line { get; }
        public RadAsmTokenType Type => AnalysisToken.Type;

        private readonly Action _navigate;

        public NavigationToken(IAnalysisToken analysisToken, string path, Action navigate)
        {
            _navigate = navigate;
            AnalysisToken = analysisToken;
            Path = path;
            Line = new TokenLine(AnalysisToken);
        }

        public void Navigate() =>
            _navigate?.Invoke();

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Path != null)
            {
                sb.Append(Path);
                sb.Append(" ");
            }
            sb.Append("(");
            sb.Append(Line.LineNumber + 1);
            sb.Append("): ");

            sb.Append(Line.LineText);
            return sb.ToString();
        }
    }
}
