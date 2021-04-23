using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Core.Tokens
{
    public interface IInstructionToken : IDefinitionToken
    {
        ICollection<IAnalysisToken> Parameters { get; }
    }

    public class InstructionToken : DefinitionToken, IInstructionToken
    {
        public InstructionToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
            : base(tokenType, trackingToken, snapshot)
        {
            Parameters = new LinkedList<IAnalysisToken>();
        }

        public ICollection<IAnalysisToken> Parameters { get; }
    }
}
