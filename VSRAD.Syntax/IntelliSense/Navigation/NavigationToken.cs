using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    public struct NavigationToken
    {
        public static NavigationToken Empty { get { return new NavigationToken(); } }

        public AnalysisToken AnalysisToken { get; }
        public ITextSnapshot Snapshot { get; }

        public NavigationToken(AnalysisToken analysisToken, ITextSnapshot version)
        {
            AnalysisToken = analysisToken;
            Snapshot = version;
        }

        public SnapshotPoint GetStart() =>
            new SnapshotPoint(Snapshot, AnalysisToken.TrackingToken.GetStart(Snapshot));

        public SnapshotPoint GetEnd() =>
            new SnapshotPoint(Snapshot, AnalysisToken.TrackingToken.GetEnd(Snapshot));

        public string GetText() =>
            AnalysisToken.TrackingToken.GetText(Snapshot);

        public static bool operator ==(NavigationToken left, NavigationToken right) =>
            left.AnalysisToken == right.AnalysisToken && left.Snapshot == right.Snapshot;

        public static bool operator !=(NavigationToken left, NavigationToken right) =>
            !(left == right);
    }
}
