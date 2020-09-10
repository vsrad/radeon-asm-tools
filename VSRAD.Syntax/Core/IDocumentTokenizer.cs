using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Threading;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public delegate void TokenizerUpdatedEventHandler(TokenizerResult result, CancellationToken cancellationToken);

    public interface IDocumentTokenizer
    {
        TokenizerResult CurrentResult { get; }
        RadAsmTokenType GetTokenType(int type);

        event TokenizerUpdatedEventHandler TokenizerUpdated;
    }
}
