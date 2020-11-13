using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public class IncludeToken : AnalysisToken
    {
        public readonly IDocument Document;

        public IncludeToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot, IDocument document)
            : base(tokenType, trackingToken, snapshot)
        {
            Document = document;
        }
    }
}
