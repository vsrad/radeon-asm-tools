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

        public int GetEnd() =>
            AnalysisToken.TrackingToken.GetEnd(Snapshot);

        public static bool operator ==(NavigationToken left, NavigationToken right) =>
            left.AnalysisToken == right.AnalysisToken && left.Snapshot == right.Snapshot;

        public static bool operator !=(NavigationToken left, NavigationToken right) =>
            !(left == right);
    }
}
