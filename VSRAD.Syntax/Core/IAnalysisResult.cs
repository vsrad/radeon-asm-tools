using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;

namespace VSRAD.Syntax.Core
{
    public interface IAnalysisResult
    {
        IReadOnlyList<IBlock> Scopes { get; }
        IReadOnlyList<IDocument> Includes { get; }
        ITextSnapshot Snapshot { get; }
    }
}
