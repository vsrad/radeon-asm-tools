using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VSRAD.Syntax.Core.Tokens
{
    public interface IDefinitionToken
    {
        ICollection<IAnalysisToken> References { get; }

        /// <summary>
        /// Return token description, for example comments 
        /// </summary>
        /// <returns>string if present; otherwise, null</returns>
        string GetDescription();
    }

    public class DefinitionToken : AnalysisToken, IDefinitionToken
    {
        public ICollection<IAnalysisToken> References { get; }

        public DefinitionToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
            : base(tokenType, trackingToken, snapshot)
        {
            References = new LinkedList<IAnalysisToken>();
        }

        public void AddReference(ReferenceToken reference) =>
            References.Add(reference);

        public string GetDescription()
        {
            return null;
        }
    }
}
