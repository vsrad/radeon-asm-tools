using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VSRAD.Syntax.Core.Tokens
{
    public class DefinitionToken : AnalysisToken
    {
        public readonly LinkedList<ReferenceToken> References;

        public DefinitionToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
            : base(tokenType, trackingToken, snapshot)
        {
            References = new LinkedList<ReferenceToken>();
        }

        public void AddReference(ReferenceToken reference) =>
            References.AddLast(reference);
    }
}
