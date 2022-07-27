using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public class AnalysisToken
    {
        public RadAsmTokenType Type { get; }
        public TrackingToken TrackingToken { get; }
        public ITextSnapshot Snapshot { get; }

        public AnalysisToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
        {
            Type = tokenType;
            TrackingToken = trackingToken;
            Snapshot = snapshot;
            Span = new SnapshotSpan(Snapshot, TrackingToken.GetSpan(Snapshot));
        }

        public SnapshotSpan Span { get; }

        public string GetText() =>
            Span.GetText();
    }
}
