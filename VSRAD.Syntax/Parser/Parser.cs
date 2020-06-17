using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.Parser
{
    public interface IParser
    {
        List<IBlock> Run(int lexerVersion, IEnumerable<TrackingToken> tokens, ITextSnapshot version, CancellationToken cancellation);
        void UpdateInstructionSet(IReadOnlyList<string> instructions);
    }

    internal abstract class Parser : IParser
    {
        protected int _currentVersion;
        protected ITextSnapshot _snapshot;
        protected HashSet<string> _instructions;

        public Parser()
        {
            _currentVersion = -1;
            _instructions = new HashSet<string>();
        }

        public List<IBlock> Run(int lexerVersion, IEnumerable<TrackingToken> tokens, ITextSnapshot snapshot, CancellationToken cancellation)
        {
            if (lexerVersion == _currentVersion)
                return null;

            _currentVersion = lexerVersion;
            _snapshot = snapshot;
            return Parse(tokens, _snapshot, cancellation);
        }

        public void UpdateInstructionSet(IReadOnlyList<string> instructions) =>
            _instructions = instructions.ToHashSet();

        public static IBlock SetBlockReady(IBlock block, List<IBlock> list)
        {
            if (block.Scope != TrackingBlock.Empty)
                list.Add(block);

            if (block.Parrent != null)
                block.Parrent.AddChildren(block);

            return block.Parrent ?? block;
        }

        public abstract List<IBlock> Parse(IEnumerable<TrackingToken> trackingTokens, ITextSnapshot version, CancellationToken cancellation);
    }
}
