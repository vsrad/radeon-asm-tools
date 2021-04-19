using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public interface ITokenizerResult
    {
        ITextSnapshot Snapshot { get; }
        ITokenizerCollection<TrackingToken> Tokens { get; }
        IList<TrackingToken> UpdatedTokens { get; }

        TrackingToken GetToken(int point);
        IEnumerable<TrackingToken> GetTokens(Span span);
    }
}
