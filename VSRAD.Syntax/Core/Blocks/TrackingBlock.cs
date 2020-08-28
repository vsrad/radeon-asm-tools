using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Blocks
{
    public struct TrackingBlock
    {
        public static TrackingBlock Empty { get { return new TrackingBlock(); } }

        public TrackingBlock(ITextSnapshot snapshot, Span span) : this()
        {
            Start = snapshot.CreateTrackingPoint(span.Start, PointTrackingMode.Positive);
            Length = span.Length;
        }

        public TrackingBlock(ITextSnapshot snapshot, int start, int end) : this()
        {
            Start = snapshot.CreateTrackingPoint(start, PointTrackingMode.Positive);
            Length = end - start;
        }

        public ITrackingPoint Start { get; }
        public int Length { get; }

        public Span GetSpan(ITextSnapshot snap) =>
            new Span(Start.GetPosition(snap), Length);

        public int GetStart(ITextSnapshot snap) =>
            Start.GetPosition(snap);

        public int GetEnd(ITextSnapshot snap) =>
            Start.GetPosition(snap) + Length;

        public static bool operator ==(TrackingBlock left, TrackingBlock right) =>
            left.Start == right.Start && left.Length == right.Length;

        public static bool operator !=(TrackingBlock left, TrackingBlock right) => !(left == right);
    }
}
