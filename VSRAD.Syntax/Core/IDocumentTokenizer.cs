using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public delegate void TokenizerUpdatedEventHandler(ITextSnapshot snapshot, IEnumerable<TrackingToken> tokens);

    public interface IDocumentTokenizer
    {
        TrackingToken GetToken(int point);
        IEnumerable<TrackingToken> GetTokens(Span span);
        RadAsmTokenType GetTokenType(int type);

        event TokenizerUpdatedEventHandler TokenizerUpdated;
    }
}
