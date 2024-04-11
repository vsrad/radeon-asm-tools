using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VSRAD.Syntax.Core.Tokens
{
    public class DocTargetListToken : AnalysisToken
    {
        public AnalysisToken Definition { get; }
        public IReadOnlyList<string> TargetList { get; }

        public DocTargetListToken(RadAsmTokenType tokenType, DefinitionToken definition, IReadOnlyList<string> targetList, ITextSnapshot snapshot)
            : base(tokenType, definition.TrackingToken, snapshot)
        {
            Definition = definition;
            TargetList = targetList;
        }
    }
}
