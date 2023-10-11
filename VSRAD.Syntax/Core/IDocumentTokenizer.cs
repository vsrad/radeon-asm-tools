using System;
using System.Threading;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public delegate void TokenizerUpdatedEventHandler(TokenizerResult result, RescanReason rescanReason, CancellationToken cancellationToken);

    public interface IDocumentTokenizer : IDisposable
    {
        TokenizerResult CurrentResult { get; }
        void Rescan(RescanReason rescanReason);
        RadAsmTokenType GetTokenType(int type);

        event TokenizerUpdatedEventHandler TokenizerUpdated;
    }
}
