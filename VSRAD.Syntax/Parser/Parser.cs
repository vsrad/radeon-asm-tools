using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Helpers;
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

        protected void AddExternalDefinitions(List<KeyValuePair<AnalysisToken, ITextSnapshot>> definitions, TrackingToken includeStr, ITextSnapshot version)
        {
            try
            {
                var filePath = Path.Combine(_documentInfo.DirectoryPath, includeStr.GetText(version).Trim('"'));
                var documentAnalysis = _documentAnalysisProvoder.GetOrCreateDocumentAnalysis(filePath);
                if (documentAnalysis != null)
                {
                    foreach (var funcToken in documentAnalysis.LastParserResult.GetGlobalTokens())
                    {
                        definitions.Add(new KeyValuePair<AnalysisToken, ITextSnapshot>(funcToken, documentAnalysis.CurrentSnapshot));
                    }
                }
            }
            catch (Exception e) when (e is ArgumentException || e is FileNotFoundException)
            {
                Error.LogError(e, "External definitions loader");
            }
        }

        protected void ParseReferenceCandidate(List<KeyValuePair<AnalysisToken, ITextSnapshot>> definitionTokens, Dictionary<string, List<KeyValuePair<IBlock, TrackingToken>>> referenceCandidate, CancellationToken cancellation)
        {
            foreach (var definitionTokenPair in definitionTokens)
            {
                cancellation.ThrowIfCancellationRequested();

                var definitionToken = definitionTokenPair.Key;
                RadAsmTokenType referenceType;
                switch (definitionToken.Type)
                {
                    case RadAsmTokenType.FunctionName:
                        referenceType = RadAsmTokenType.FunctionReference;
                        break;
                    case RadAsmTokenType.Label:
                        referenceType = RadAsmTokenType.LabelReference;
                        break;
                    case RadAsmTokenType.GlobalVariable:
                        referenceType = RadAsmTokenType.GlobalVariableReference;
                        break;
                    default:
                        continue; // skip unknown token
                }

                var tokenText = definitionToken.TrackingToken.GetText(definitionTokenPair.Value);
                if (referenceCandidate.TryGetValue(tokenText, out var referenceTokenPairs))
                {
                    foreach (var referenceTokenPair in referenceTokenPairs)
                        referenceTokenPair.Key.Tokens.Add(new ReferenceToken(referenceType, referenceTokenPair.Value, definitionToken));
                }
            }
        }
    }
}
