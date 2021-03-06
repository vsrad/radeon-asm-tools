﻿using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public class TokenizerResult : ITokenizerResult
    {
        public ITextSnapshot Snapshot { get; }
        public ITokenizerCollection<TrackingToken> Tokens { get; }
        public IList<TrackingToken> UpdatedTokens { get; }

        public TokenizerResult(ITextSnapshot snapshot,
            ITokenizerCollection<TrackingToken> tokens,
            IList<TrackingToken> updatedTokens)
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
    }
}
