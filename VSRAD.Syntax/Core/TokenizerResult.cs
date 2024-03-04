using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public sealed class TokenizerResult
    {
        public ITextSnapshot Snapshot { get; }
        public ITokenizerCollection<TrackingToken> Tokens { get; }
        public IReadOnlyList<TrackingToken> UpdatedTokens { get; }
        public Span UpdatedTokenSpan { get; }

        public TokenizerResult(ITextSnapshot snapshot,
            ITokenizerCollection<TrackingToken> tokens,
            IReadOnlyList<TrackingToken> updatedTokens)
        {
            Snapshot = snapshot;
            Tokens = tokens;
            UpdatedTokens = updatedTokens;
            if (updatedTokens.Count != 0)
            {
                var updatedTokenSpanStart = updatedTokens[0].GetStart(snapshot);
                var updatedTokenSpanEnd = updatedTokens[updatedTokens.Count - 1].GetEnd(snapshot);
                UpdatedTokenSpan = Span.FromBounds(updatedTokenSpanStart, updatedTokenSpanEnd);
            }
            else
            {
                UpdatedTokenSpan = new Span();
            }
        }

        public TrackingToken GetToken(int point) =>
            Tokens.GetCoveringToken(Snapshot, point);

        public IEnumerable<TrackingToken> GetTokens(Span span)
        {
            if (span.Length == 0)
            {
                if (span.Start == 0 && span.End == 0)
                    return Enumerable.Empty<TrackingToken>();

                return new[] { GetToken(span.Start) };
            }

            return Tokens.GetCoveringTokens(Snapshot, span);
        }
    }
}
