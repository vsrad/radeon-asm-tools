using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core.Parser
{
    public interface ICodeParser : IParser
    {
        void UpdateInstructions(IEnumerable<Instruction> instructions, IEnumerable<Instruction> selectedSetInstructions);
    }

    internal abstract class AbstractCodeParser : AbstractParser, ICodeParser
    {
        protected HashSet<string> Instructions { get; private set; }
        protected HashSet<string> OtherInstructions { get; private set; }

        private readonly IDocumentFactory _documentFactory;
        protected readonly DefinitionContainer _definitionContainer;

        public AbstractCodeParser(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
            _definitionContainer = new DefinitionContainer();
            Instructions = new HashSet<string>();
            OtherInstructions = new HashSet<string>();
        }

        public void UpdateInstructions(IEnumerable<Instruction> instructions, IEnumerable<Instruction> selectedSetInstructions)
        {
            Instructions = selectedSetInstructions
                .Select(i => i.Text)
                .Distinct()
                .ToHashSet();

            OtherInstructions = instructions
                .Select(i => i.Text)
                .Distinct()
                .ToHashSet();

            OtherInstructions.ExceptWith(Instructions);
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
