using Microsoft.VisualStudio.Text;
using System;
using System.Text;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    public readonly struct NavigationToken : IEquatable<NavigationToken>
    {
        public static NavigationToken Empty { get { return new NavigationToken(); } }

        public AnalysisToken AnalysisToken { get; }
        public ITextSnapshotLine SnapshotLine { get; }
        public string Path { get; }
        public int Line { get; }
        public string LineText => SnapshotLine.GetText();
        public RadAsmTokenType Type => AnalysisToken.Type;

        private readonly Action _navigate;

        public NavigationToken(AnalysisToken analysisToken, string path, Action navigate)
        {
            AnalysisToken = analysisToken;
            SnapshotLine = analysisToken.Span.Start.GetContainingLine();
            Path = path;
            Line = SnapshotLine.LineNumber;

            _navigate = navigate;
        }

        public void Navigate() =>
            _navigate?.Invoke();

        public SnapshotPoint GetEnd() =>
            AnalysisToken.Span.End;

        public string GetText() =>
            AnalysisToken.GetText();

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Path != null)
            {
                sb.Append(Path);
                sb.Append(" ");
            }
            sb.Append("(");
            sb.Append(Line + 1);
            sb.Append("): ");

            sb.Append(LineText);
            return sb.ToString();
        }

        public bool Equals(NavigationToken o) => AnalysisToken == o.AnalysisToken && Path == o.Path && Line == o.Line;

        public static bool operator ==(NavigationToken left, NavigationToken right) =>
            left.Equals(right);

        public static bool operator !=(NavigationToken left, NavigationToken right) =>
            !(left == right);

        public override bool Equals(object obj) => obj is NavigationToken o && Equals(o);

        public override int GetHashCode() => (AnalysisToken, Path, Line).GetHashCode();
    }
}
