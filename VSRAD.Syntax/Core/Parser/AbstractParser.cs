using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Parser
{
    public interface IParser
    {
        Task<List<IBlock>> RunAsync(IDocument document, ITextSnapshot version, IEnumerable<TrackingToken> tokens, CancellationToken cancellation);
    }

    internal abstract class AbstractParser : IParser
    {
        private readonly IDocumentFactory _documentFactory;

        public AbstractParser(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
        }

        public abstract Task<List<IBlock>> RunAsync(IDocument document, ITextSnapshot version, IEnumerable<TrackingToken> tokens, CancellationToken cancellation);

        protected static IBlock SetBlockReady(IBlock block, List<IBlock> list)
        {
            if (block.Scope != TrackingBlock.Empty)
                list.Add(block);

            if (block.Parrent != null)
                block.Parrent.AddChildren(block);

            return block.Parrent ?? block;
        }

        protected async Task AddExternalDefinitionsAsync(string path, ITextSnapshot version, List<DefinitionToken> definitions, TrackingToken includeStr)
        {
            try
            {
                var externalFileName = includeStr.GetText(version).Trim('"');
                var externalFilePath = Path.Combine(Path.GetDirectoryName(path), externalFileName);
                var externalDocument = _documentFactory.GetOrCreateDocument(externalFilePath);

                if (externalDocument != null)
                {
                    var externalDocumentAnalysis = externalDocument.DocumentAnalysis;
                    var externalAnalysisResult = await externalDocumentAnalysis
                        .GetAnalysisResultAsync(externalDocument.CurrentSnapshot)
                        .ConfigureAwait(false);

                    definitions.AddRange(externalAnalysisResult.GetGlobalDefinitions());
                }
            }
            catch (Exception e) when (e is ArgumentException || e is FileNotFoundException) { /* invalid path */ }
        }

        protected void ParseReferenceCandidate(List<DefinitionToken> definitionTokens, Dictionary<string, List<KeyValuePair<IBlock, TrackingToken>>> referenceCandidate, ITextSnapshot snapshot, CancellationToken cancellation)
        {
            foreach (var definitionToken in definitionTokens)
            {
                cancellation.ThrowIfCancellationRequested();

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

                var tokenText = definitionToken.GetText();
                if (referenceCandidate.TryGetValue(tokenText, out var referenceTokenPairs))
                {
                    foreach (var referenceTokenPair in referenceTokenPairs)
                        referenceTokenPair.Key.Tokens.Add(new ReferenceToken(referenceType, referenceTokenPair.Value, snapshot, definitionToken));
                }
            }
        }
    }
}
