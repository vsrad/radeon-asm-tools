using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VSRAD.Syntax.Core.Tokens
{
    public interface IDefinitionToken : IAnalysisToken
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
        private readonly string _text;

        public DefinitionToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
            : base(tokenType, trackingToken, snapshot)
        {
            References = new LinkedList<IAnalysisToken>();
            _text = base.GetText();
        }

        public void AddReference(ReferenceToken reference) =>
            References.Add(reference);

        public override string GetText() => _text;

        public string GetDescription()
        {
            return null;
        }
    }
}
