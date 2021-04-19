using System.Threading;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public delegate void TokenizerUpdatedEventHandler(ITokenizerResult result, CancellationToken cancellationToken);

    public interface IDocumentTokenizer
    {
        ITokenizerResult CurrentResult { get; }
        RadAsmTokenType GetTokenType(int type);
        void OnDestroy();

        event TokenizerUpdatedEventHandler TokenizerUpdated;
    }
}
