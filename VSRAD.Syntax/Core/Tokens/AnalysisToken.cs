using System;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public interface IAnalysisToken
    {
        RadAsmTokenType Type { get; }
        TrackingToken TrackingToken { get; }
        ITextSnapshot Snapshot { get; }
        SnapshotSpan Span { get; }
        string Text { get; }
    }

    public class AnalysisToken : IAnalysisToken
    {
        public RadAsmTokenType Type { get; }
        public TrackingToken TrackingToken { get; }
        public ITextSnapshot Snapshot { get; }
        public SnapshotSpan Span { get; }
        public string Text => _textLazy.Value;

        private readonly Lazy<string> _textLazy;

        public AnalysisToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
        {
            Type = tokenType;
            TrackingToken = trackingToken;
            Snapshot = snapshot;
            Span = new SnapshotSpan(Snapshot, TrackingToken.GetSpan(Snapshot));
            _textLazy = new Lazy<string>(() => Span.GetText());
        }
    }
}
