namespace VSRAD.Syntax.Core.Tokens
{
    public class VariableToken : AnalysisToken
    {
        public TrackingToken DefaultValue { get; }

        public VariableToken(RadAsmTokenType type, TrackingToken token, TrackingToken defaultValue = default) : base(type, token)
        {
            DefaultValue = defaultValue;
        }
    }
}
