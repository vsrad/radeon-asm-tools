using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Blocks;

namespace VSRAD.Syntax.Core.Tokens
{
    public interface IFunctionToken : IDefinitionToken
    {
        IFunctionBlock FunctionBlock { get; }
    }

    internal class FunctionToken : DefinitionToken, IFunctionToken
    {
        public FunctionToken(RadAsmTokenType tokenType, TrackingToken trackingToken, ITextSnapshot snapshot)
            : base(tokenType, trackingToken, snapshot) { }

        public IFunctionBlock FunctionBlock { get; set; }
    }
}
