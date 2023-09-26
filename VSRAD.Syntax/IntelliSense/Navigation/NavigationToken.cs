using Microsoft.VisualStudio.Text;
using System;
using System.Text;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    public readonly struct NavigationToken : IEquatable<NavigationToken>
    {
        public IDocument Document { get; }
        public AnalysisToken AnalysisToken { get; }
        public ITextSnapshotLine SnapshotLine { get; }

        public int Line => SnapshotLine.LineNumber;
        public string LineText => SnapshotLine.GetText();
        public string Path => Document.Path;
        public RadAsmTokenType Type => AnalysisToken.Type;

        public NavigationToken(IDocument document, AnalysisToken analysisToken)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            AnalysisToken = analysisToken ?? throw new ArgumentNullException(nameof(analysisToken));
            SnapshotLine = analysisToken.Span.Start.GetContainingLine();
        }

        public void Navigate()
        {
            try
            {
                // cannot use AnalysisToken.SpanStart because it's assigned to snapshot which may be outdated
                var navigatePosition = AnalysisToken.TrackingToken.GetEnd(Document.CurrentSnapshot);
                Document.NavigateToPosition(navigatePosition);
            }
            catch (Exception e)
            {
                Error.ShowError(e, "Navigation");
            }
        }

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

        public bool Equals(NavigationToken o) => Document == o.Document && AnalysisToken == o.AnalysisToken;

        public static bool operator ==(NavigationToken left, NavigationToken right) => left.Equals(right);

        public static bool operator !=(NavigationToken left, NavigationToken right) => !(left == right);

        public override bool Equals(object obj) => obj is NavigationToken o && Equals(o);

        public override int GetHashCode() => (Document, AnalysisToken).GetHashCode();
    }
}
