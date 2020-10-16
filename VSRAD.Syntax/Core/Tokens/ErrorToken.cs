using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public interface IErrorToken
    {
        ITextSnapshot Snapshot { get; }
        SnapshotSpan Span { get; }
        string Message { get; }
    }

    internal class ErrorToken : IErrorToken
    {
        public ITextSnapshot Snapshot { get; }
        public SnapshotSpan Span { get; }
        public string Message { get; }

        public ErrorToken(TrackingToken trackingToken, ITextSnapshot snapshot, string message)
        {
            Snapshot = snapshot;
            Span = new SnapshotSpan(Snapshot, trackingToken.GetSpan(Snapshot));
            Message = message;
        }
    }
}
