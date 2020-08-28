namespace VSRAD.Syntax.Core.Tokens
{
    internal class IncludeToken : AnalysisToken
    {
        public readonly IDocument Document;

        public IncludeToken(RadAsmTokenType tokenType, TrackingToken trackingToken, IDocument document)
            : base(tokenType, trackingToken)
        {
            Document = document;
        }
    }
}
