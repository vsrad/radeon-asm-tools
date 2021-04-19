using System;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public class IncludeToken : AnalysisToken
    {
        public readonly IDocument IncludeDocument;

        public IncludeToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
            : base(tokenType, trackingToken, snapshot)
        {
            throw new NotImplementedException();
        }
    }
}
