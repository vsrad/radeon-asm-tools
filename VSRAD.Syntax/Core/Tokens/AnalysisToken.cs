using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public interface IAnalysisToken
    {
        RadAsmTokenType Type { get; }
        TrackingToken TrackingToken { get; }
        SnapshotSpan Span { get; }

        string GetText();
    }

    public class AnalysisToken : IAnalysisToken
    {
        public RadAsmTokenType Type { get; }
        public TrackingToken TrackingToken { get; }
        public SnapshotSpan Span { get; }

        public AnalysisToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
        {
            Type = tokenType;
            TrackingToken = trackingToken;
            Span = new SnapshotSpan(snapshot, TrackingToken.GetSpan(snapshot));
        }

        public virtual string GetText() =>
            Span.GetText();
    }
}
