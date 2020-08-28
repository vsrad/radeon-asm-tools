using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Blocks;

namespace VSRAD.Syntax.Core
{
    internal class AnalysisResult : IAnalysisResult
    {
        public AnalysisResult(IReadOnlyList<IBlock> scopes, IReadOnlyList<IDocument> includes, ITextSnapshot snapshot)
        {
            Scopes = scopes;
            Includes = includes;
            Snapshot = snapshot;
        }

        public IReadOnlyList<IBlock> Scopes { get; private set; }

        public IReadOnlyList<IDocument> Includes { get; private set; }

        public ITextSnapshot Snapshot { get; private set; }
    }
}
