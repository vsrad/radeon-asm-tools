using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    internal class AnalysisResult : IAnalysisResult
    {
        public AnalysisResult(IBlock root, 
            IReadOnlyList<IBlock> scopes, 
            IReadOnlyList<IDocument> includes, 
            ITextSnapshot snapshot)
        {
            Root = root;
            Scopes = scopes;
            Includes = includes;
            Snapshot = snapshot;
        }

        public IBlock Root { get; private set; }
        public IReadOnlyList<IBlock> Scopes { get; private set; }
        public IReadOnlyList<IDocument> Includes { get; private set; }
        public ITextSnapshot Snapshot { get; private set; }

        public AnalysisToken GetToken(int point)
        {
            var block = GetBlock(point);

            foreach (var token in block.Tokens)
            {
                if (token.GetSpan().Contains(point))
                    return token;
            }

            return null;
        }

        public IBlock GetBlock(int point)
        {
            var block = Root;
            while (true)
            {
                var continueSearch = false;
                foreach (var innerBlock in block.Childrens)
                {
                    if (innerBlock.Type == BlockType.Comment) 
                        continue;
                    if (innerBlock.InRange(point))
                    {
                        block = innerBlock;
                        continueSearch = true;
                        break;
                    }
                }

                if (!continueSearch)
                    break;
            }
            return block;
        }

        public IEnumerable<DefinitionToken> GetGlobalDefinitions() =>
            Root.Tokens.Where(t => t is DefinitionToken).Cast<DefinitionToken>();
    }
}
