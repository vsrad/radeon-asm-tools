using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    internal class AnalysisResult : IAnalysisResult
    {
        public AnalysisResult(IDocument document, IParserResult parserResult,
            IReadOnlyList<IDocument> includes, 
            ITextSnapshot snapshot)
        {
            Document = document;
            Root = parserResult.RootBlock;
            Scopes = parserResult.Blocks;
            Errors = parserResult.Errors;
            Includes = includes;
            Snapshot = snapshot;
        }

        public IDocument Document { get; }
        public IBlock Root { get; }
        public IReadOnlyList<IBlock> Scopes { get; }
        public IReadOnlyList<IErrorToken> Errors { get; }
        public IReadOnlyList<IDocument> Includes { get; }
        public ITextSnapshot Snapshot { get; }

        public IAnalysisToken GetToken(int point)
        {
            var block = GetBlock(point);
            return block.Tokens.FirstOrDefault(token => token.Span.Contains(point));
        }

        public IBlock GetBlock(int point)
        {
            var block = Root;
            while (true)
            {
                var innerBlock = InnerInRange(block.Children, point);
                if (innerBlock != null)
                {
                    block = innerBlock;
                    continue;
                }

                break;
            }
            return block;
        }

        private static IBlock InnerInRange(IEnumerable<IBlock> blocks, int point) => 
            blocks.Where(innerBlock => innerBlock.Type != BlockType.Comment).FirstOrDefault(innerBlock => innerBlock.InRange(point));

        public IEnumerable<DefinitionToken> GetGlobalDefinitions() =>
            Root.Tokens.Where(t => t is DefinitionToken).Cast<DefinitionToken>();
    }
}
