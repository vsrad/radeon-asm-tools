using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public sealed class AnalysisResult
    {
        public IBlock Root { get; }
        public IReadOnlyList<IBlock> Scopes { get; }
        public IReadOnlyList<IErrorToken> Errors { get; }
        public ITextSnapshot Snapshot { get; }

        public AnalysisResult(ParserResult parserResult, ITextSnapshot snapshot)
        {
            Root = parserResult.RootBlock;
            Scopes = parserResult.Blocks;
            Errors = parserResult.Errors;
            Snapshot = snapshot;
        }

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

        private static IBlock InnerInRange(IEnumerable<IBlock> blocks, int point)
        {
            foreach (var innerBlock in blocks)
            {
                if (innerBlock.Type == BlockType.Comment) continue;
                if (innerBlock.InRange(point)) return innerBlock;
            }

            return null;
        }

        public IEnumerable<DefinitionToken> GetGlobalDefinitions() =>
            Root.Tokens.Where(t => t is DefinitionToken).Cast<DefinitionToken>();
    }
}
