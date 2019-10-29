using VSRAD.Syntax.Parser.Tokens;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VSRAD.Syntax.Parser.Blocks
{
    internal sealed class RootBlock : BaseBlock
    {
        public RootBlock(ITextSnapshot textSnapshot) : base(null, BlockType.Root, default)
        {
            FunctionTokens = new List<FunctionToken>();
            BlockSpan = new SnapshotSpan(textSnapshot, 0, 0);
            BlockActualSpan = new SnapshotSpan(textSnapshot, 0, textSnapshot.Length);
            BlockReady = true;
        }

        public IList<FunctionToken> FunctionTokens { get; private set; }
    }
}
