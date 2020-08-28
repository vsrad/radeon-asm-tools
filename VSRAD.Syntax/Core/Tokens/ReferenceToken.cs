namespace VSRAD.Syntax.Core.Tokens
{
    internal class ReferenceToken : AnalysisToken
    {
        public readonly AnalysisToken Definition;

        public ReferenceToken(RadAsmTokenType tokenType, TrackingToken trackingToken, AnalysisToken definitionToken)
            : base (tokenType, trackingToken)
        {
            Definition = definitionToken;
        }
    }
}
