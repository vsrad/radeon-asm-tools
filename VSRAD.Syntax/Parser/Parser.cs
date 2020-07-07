using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Diagnostics;
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
        protected readonly DocumentInfo _documentInfo;
        protected readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        protected int _currentVersion;
        protected HashSet<string> _instructions;
        private bool _engagedParsing;

        public Parser(DocumentInfo documentInfo, DocumentAnalysisProvoder documentAnalysisProvoder)
        {
            _documentInfo = documentInfo;
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _currentVersion = -1;
            _engagedParsing = false;
            _instructions = new HashSet<string>();
        }

        public List<IBlock> Run(int lexerVersion, IEnumerable<TrackingToken> tokens, ITextSnapshot snapshot, CancellationToken cancellation)
        {
            if (lexerVersion == _currentVersion)
                return null;

            // Cycles may occur using the include keywords
            // With this event, parsing is not performed
            if (_engagedParsing)
                return null;

            _currentVersion = lexerVersion;
            try
            {
                _engagedParsing = true;
                return Parse(tokens, snapshot, cancellation);
            }
            finally
            {
                _engagedParsing = false;
            }
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
