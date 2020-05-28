namespace VSRAD.Syntax.Parser.Tokens
{
    public struct AnalysisToken
    {
        public static AnalysisToken Empty { get { return new AnalysisToken(); } }

        public RadAsmTokenType Type { get; }
        public TrackingToken TrackingToken { get; }

        public AnalysisToken(RadAsmTokenType tokenType, TrackingToken trackingToken)
        {
            Type = tokenType;
            TrackingToken = trackingToken;
        }

        public static bool operator ==(AnalysisToken left, AnalysisToken right) =>
            left.Type == right.Type && left.TrackingToken == right.TrackingToken;

        public static bool operator !=(AnalysisToken left, AnalysisToken right) => !(left == right);
    }
}
