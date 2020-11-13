using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public class ReferenceToken : AnalysisToken
    {
        public readonly DefinitionToken Definition;

        public ReferenceToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot, DefinitionToken definitionToken)
            : base (tokenType, trackingToken, snapshot)
        {
            Definition = definitionToken;
            Definition.AddReference(this);
        }
    }
}
