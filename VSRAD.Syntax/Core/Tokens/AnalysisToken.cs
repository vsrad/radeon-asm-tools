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
        }

        public SnapshotSpan GetSpan() =>
            new SnapshotSpan(Snapshot, TrackingToken.GetSpan(Snapshot));

        public SnapshotPoint GetStart() =>
            new SnapshotPoint(Snapshot, TrackingToken.GetStart(Snapshot));

        public int GetEnd() =>
            new SnapshotPoint(Snapshot, TrackingToken.GetEnd(Snapshot));

        public string GetText() =>
            GetSpan().GetText();
    }
}
