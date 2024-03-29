﻿using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;

namespace VSRAD.Syntax.Core.Tokens
{
    public readonly struct TrackingToken : IEquatable<TrackingToken>
    {
        public static TrackingToken Empty { get { return new TrackingToken(); } }

        public class NonOverlappingComparer : IComparer<TrackingToken>
        {
            public ITextSnapshot Version;

            public int Compare(TrackingToken x, TrackingToken y)
            {
                int result = x.Start.GetPosition(Version).CompareTo(y.Start.GetPosition(Version));
                return result;
            }
        }

        public ITrackingPoint Start { get; }
        public int Length { get; }
        public int Type { get; }
        public bool IsEmpty { get { return Start == null; } }

        public TrackingToken(ITextSnapshot snapshot, TokenSpan tokenSpan) : this()
        {
            Start = snapshot.CreateTrackingPoint(tokenSpan.Span.Start, PointTrackingMode.Positive);
            Length = tokenSpan.Span.Length;
            Type = tokenSpan.Type;
        }

        public TrackingToken(ITextSnapshot snapshot, Span span, int type) : this()
        {
            Start = snapshot.CreateTrackingPoint(span.Start, PointTrackingMode.Positive);
            Length = span.Length;
            Type = type;
        }

        public Span GetSpan(ITextSnapshot snap) =>
            new Span(Start.GetPosition(snap), Length);

        public int GetStart(ITextSnapshot snap) =>
            Start.GetPosition(snap);

        public int GetEnd(ITextSnapshot snap) =>
            Start.GetPosition(snap) + Length;

        public string GetText(ITextSnapshot snap) =>
            snap.GetText(GetSpan(snap));

        public bool Equals(TrackingToken o) => Type == o.Type && Length == o.Length && Start == o.Start;

        public static bool operator ==(TrackingToken left, TrackingToken right) => left.Equals(right);

        public static bool operator !=(TrackingToken left, TrackingToken right) => !(left == right);

        public override bool Equals(object obj) => obj is TrackingToken o && Equals(o);

        public override int GetHashCode() => (Type, Length, Start).GetHashCode();
    }
}
