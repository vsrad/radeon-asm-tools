using System.Threading;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public delegate void TokenizerUpdatedEventHandler(ITokenizerResult result, RescanReason rescanReason, CancellationToken cancellationToken);

    public interface IDocumentTokenizer
    {
        ITokenizerResult CurrentResult { get; }
        void Rescan(RescanReason rescanReason);
        RadAsmTokenType GetTokenType(int type);

        event TokenizerUpdatedEventHandler TokenizerUpdated;
    }
}
