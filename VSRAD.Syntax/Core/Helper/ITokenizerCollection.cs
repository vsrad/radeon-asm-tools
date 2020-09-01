using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Helper
{
    public interface ITokenizerCollection<T> : IEnumerable<T>
    {
        TrackingToken GetCoveringToken(ITextSnapshot version, int pos);
        IEnumerable<T> GetCoveringTokens(ITextSnapshot version, Span span);
        List<T> GetInvalidated(ITextSnapshot version, Span span);
        IEnumerable<T> InOrderAfter(ITextSnapshot version, int start);
    }
}
