using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public struct TokenizerResult : IEquatable<TokenizerResult>
    {
        public ITextSnapshot Snapshot;
        public ITokenizerCollection<TrackingToken> Tokens;
        public IEnumerable<TrackingToken> UpdatedTokens;

        public TokenizerResult(ITextSnapshot snapshot, 
            ITokenizerCollection<TrackingToken> tokens, 
            IEnumerable<TrackingToken> updatedTokens)
        {
            Snapshot = snapshot;
            Tokens = tokens;
            UpdatedTokens = updatedTokens;
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

        public bool Equals(TokenizerResult m) => Snapshot == m.Snapshot && Tokens == m.Tokens && UpdatedTokens == m.UpdatedTokens;
        public override bool Equals(object obj) => obj is TokenizerResult && Equals(obj);
        public static bool operator ==(TokenizerResult left, TokenizerResult right) => left.Equals(right);
        public static bool operator !=(TokenizerResult left, TokenizerResult right) => !(left == right);
        public override int GetHashCode() => (Snapshot, Tokens, UpdatedTokens).GetHashCode();
    }
}
