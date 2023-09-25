using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.IntelliSense.Navigation;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Options.Instructions;
using VSRAD.Syntax.IntelliSense.Navigation.NavigationList;
using System;
using System.Linq;
using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.IntelliSense
{
    public interface IIntelliSenseService
    {
        NavigationToken CreateToken(AnalysisToken analysisToken, string path);
        NavigationToken CreateToken(AnalysisToken analysisToken, IDocument document);
        Task<IntelliSenseToken> GetIntelliSenseTokenAsync(SnapshotPoint point);
        void NavigateOrOpenNavigationList(IReadOnlyList<NavigationToken> navigations);
    }

    [Export(typeof(IIntelliSenseService))]
    internal class IntelliSenseService : IIntelliSenseService
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly IInstructionListManager _instructionListManager;

        [ImportingConstructor]
        public IntelliSenseService(
            IDocumentFactory documentFactory,
            IInstructionListManager instructionListManager)
        {
            _documentFactory = documentFactory;
            _instructionListManager = instructionListManager;
        }

        public NavigationToken CreateToken(AnalysisToken analysisToken, string path)
        {
            var document = _documentFactory.GetOrCreateDocument(analysisToken.Snapshot.TextBuffer);
            return CreateToken(analysisToken, document);
        }

        public NavigationToken CreateToken(AnalysisToken analysisToken, IDocument document)
        {
            if (document == null) return NavigationToken.Empty;

            var navigateAction = GetNavigateAction(analysisToken, document);
            return new NavigationToken(analysisToken, document.Path, navigateAction);
        }

        private Action GetNavigateAction(AnalysisToken analysisToken, IDocument document) =>
            () =>
            {
                try
                {
                    // cannot use AnalysisToken.SpanStart because it's assigned to snapshot which may be outdated
                    var navigatePosition = analysisToken.TrackingToken.GetEnd(document.CurrentSnapshot);
                    document.NavigateToPosition(navigatePosition);
                }
                catch (Exception e)
                {
                    Error.ShowError(e, "Navigation service");
                }
            };

        public async Task<IntelliSenseToken> GetIntelliSenseTokenAsync(SnapshotPoint point)
        {
            var document = _documentFactory.GetOrCreateDocument(point.Snapshot.TextBuffer);
            if (document == null) return null;

            var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(point.Snapshot);
            var symbol = analysisResult.GetToken(point);
            if (symbol == null) return null;

            if (symbol is DefinitionToken definition)
            {
                return new IntelliSenseToken(symbol, new[] { CreateToken(definition, document) });
            }
            else if (symbol is ReferenceToken reference)
            {
                var definitionDocument = _documentFactory.GetOrCreateDocument(reference.Definition.Snapshot.TextBuffer);
                return new IntelliSenseToken(symbol, new[] { CreateToken(reference.Definition, definitionDocument) });
            }
            else if (symbol.Type == RadAsmTokenType.Instruction)
            {
                var asmType = symbol.Snapshot.GetAsmType();
                var instructions = _instructionListManager.GetSelectedSetInstructions(asmType);
                var instructionText = symbol.GetText().TrimPrefix("#");
                var definitions = instructions.Where(i => i.Text == instructionText).SelectMany(i => i.Navigations).ToList();
                if (definitions.Count != 0)
                    return new IntelliSenseToken(symbol, definitions);
            }

            return null;
        }

        public void NavigateOrOpenNavigationList(IReadOnlyList<NavigationToken> navigations)
        {
            if (navigations.Count == 1) navigations[0].Navigate();
            else if (navigations.Count > 1) NavigationList.UpdateNavigationList(navigations);
            else Error.ShowWarningMessage("Cannot navigate to the symbol under the cared");
        }
    }
}