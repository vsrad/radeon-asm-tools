namespace VSRAD.Syntax.Core.Tokens
{
    public class AnalysisToken
    {
        public RadAsmTokenType Type { get; }
        public TrackingToken TrackingToken { get; }

        public AnalysisToken(RadAsmTokenType tokenType, TrackingToken trackingToken)
        {
            Type = tokenType;
            TrackingToken = trackingToken;
        }
    }
}
