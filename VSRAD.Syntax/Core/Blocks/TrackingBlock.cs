using Microsoft.VisualStudio.Text;
using System;

namespace VSRAD.Syntax.Core.Blocks
{
    public readonly struct TrackingBlock : IEquatable<TrackingBlock>
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

        public bool Equals(TrackingBlock o) => Start == o.Start && Length == o.Length;

        public static bool operator ==(TrackingBlock left, TrackingBlock right) => left.Equals(right);

        public static bool operator !=(TrackingBlock left, TrackingBlock right) => !(left == right);

        public override bool Equals(object obj) => obj is TrackingBlock o && Equals(o);

        public override int GetHashCode() => (Start, Length).GetHashCode();
    }
}
