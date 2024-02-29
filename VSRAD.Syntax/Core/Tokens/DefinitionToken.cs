using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VSRAD.Syntax.Core.Tokens
{
    public class DefinitionToken : AnalysisToken
    {
        public List<ReferenceToken> References => _references;
        private readonly List<ReferenceToken> _references = new List<ReferenceToken>();

        public DefinitionToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
            : base(tokenType, trackingToken, snapshot)
        {
        }

        public void AddReference(ReferenceToken reference) =>
            _references.Add(reference);
    }
}
