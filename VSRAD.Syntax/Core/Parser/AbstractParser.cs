using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Parser
{
    public interface IParser
    {
        Task<List<IBlock>> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> tokens, CancellationToken cancellation);
    }

    internal abstract class AbstractParser : IParser
    {
        private readonly IDocumentFactory _documentFactory;
        protected readonly DefinitionContainer _definitionContainer;

        public AbstractParser(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
            _definitionContainer = new DefinitionContainer();
        }

        public abstract Task<List<IBlock>> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> tokens, CancellationToken cancellation);

        protected static IBlock SetBlockReady(IBlock block, List<IBlock> list)
        {
            if (block.Scope != TrackingBlock.Empty)
                list.Add(block);

            if (block.Parent != null)
                block.Parent.AddChildren(block);

            return block.Parent ?? block;
        }

        protected async Task AddExternalDefinitionsAsync(string path, TrackingToken includeStr, IBlock block)
        {
            try
            {
                var externalFileName = includeStr.GetText(block.Snapshot).Trim('"');
                var externalFilePath = Path.Combine(Path.GetDirectoryName(path), externalFileName);
                var externalDocument = _documentFactory.GetOrCreateDocument(externalFilePath);

                if (externalDocument != null)
                {
                    var externalDocumentAnalysis = externalDocument.DocumentAnalysis;
                    var externalAnalysisResult = await externalDocumentAnalysis
                        .GetAnalysisResultAsync(externalDocument.CurrentSnapshot)
                        .ConfigureAwait(false);

                    foreach (var externalDefinition in externalAnalysisResult.GetGlobalDefinitions())
                        _definitionContainer.Add(block, externalDefinition);
                }
            }
            catch (Exception e) when (e is ArgumentException || e is FileNotFoundException) { /* invalid path */ }
        }

        protected bool TryAddReference(string tokenText, TrackingToken token, IBlock block, ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_definitionContainer.TryGetDefinition(tokenText, out var definitionToken))
            {
                RadAsmTokenType referenceType;
                switch (definitionToken.Type)
                {
                    case RadAsmTokenType.FunctionName:
                        referenceType = RadAsmTokenType.FunctionReference;
                        break;
                    case RadAsmTokenType.FunctionParameter:
                        referenceType = RadAsmTokenType.FunctionParameterReference;
                        break;
                    case RadAsmTokenType.Label:
                        referenceType = RadAsmTokenType.LabelReference;
                        break;
                    case RadAsmTokenType.GlobalVariable:
                        referenceType = RadAsmTokenType.GlobalVariableReference;
                        break;
                    case RadAsmTokenType.LocalVariable:
                        referenceType = RadAsmTokenType.LocalVariableReference;
                        break;
                    default: return true;
                }

                block.AddToken(new ReferenceToken(referenceType, token, version, definitionToken));
                return true;
            }

            return false;
        }
    }
}
