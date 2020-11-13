using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public class VariableToken : DefinitionToken
    {
        public TrackingToken DefaultValue { get; }

        public VariableToken(RadAsmTokenType type, TrackingToken token, ITextSnapshot snapshot, TrackingToken defaultValue = default) 
            : base(type, token, snapshot)
        {
            DefaultValue = defaultValue;
        }
    }
}
