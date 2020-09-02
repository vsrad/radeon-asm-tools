using EnvDTE;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public interface IParser
    {
        Task<List<IBlock>> RunAsync(IEnumerable<TrackingToken> tokens, ITextSnapshot version, CancellationToken cancellation);
    }

    internal abstract class AbstractParser : IParser
    {
        private readonly IDocumentFactory _documentFactory;

        public AbstractParser(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
        }

        public abstract Task<List<IBlock>> RunAsync(IEnumerable<TrackingToken> tokens, ITextSnapshot snapshot, CancellationToken cancellation);

        protected static IBlock SetBlockReady(IBlock block, List<IBlock> list)
        {
            if (block.Scope != TrackingBlock.Empty)
                list.Add(block);

            if (block.Parrent != null)
                block.Parrent.AddChildren(block);

            return block.Parrent ?? block;
        }

        protected async Task AddExternalDefinitionsAsync(List<KeyValuePair<AnalysisToken, ITextSnapshot>> definitions, TrackingToken includeStr, ITextSnapshot version)
        {
            try
            {
                //var filePath = Path.Combine(Path.GetDirectoryName(_document.Path), includeStr.GetText(version).Trim('"'));
                //var externalDocument = _documentFactory.GetOrCreateDocument(filePath);

                //if (externalDocument != null)
                //{
                //    var externalDocumentAnalysis = externalDocument.DocumentAnalysis;
                //    var analysisResult = await externalDocumentAnalysis.GetAnalysisResultAsync(externalDocument.CurrentSnapshot);

                //    foreach (var tokens in analysisResult.Scopes[0].Tokens)
                //    {
                //        definitions.Add(new KeyValuePair<AnalysisToken, ITextSnapshot>(funcToken, documentAnalysis.CurrentSnapshot));
                //    }
                //}
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
