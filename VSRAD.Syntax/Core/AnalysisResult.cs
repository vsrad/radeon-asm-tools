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

        public IBlock Root { get; }
        public IReadOnlyList<IBlock> Scopes { get; }
        public IReadOnlyList<IDocument> Includes { get; }
        public ITextSnapshot Snapshot { get; }

        public AnalysisToken GetToken(int point)
        {
            var block = GetBlock(point);

            foreach (var token in block.Tokens)
            {
                if (token.Span.Contains(point))
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
                foreach (var innerBlock in block.Children)
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
