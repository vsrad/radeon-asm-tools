using System.Collections.Generic;
using System.Windows.Documents;

namespace VSRAD.Syntax.IntelliSense.FindReferences
{
    public interface IInlinedEntry
    {
        IList<Inline> CreateLineTextInlines();
    }
}
