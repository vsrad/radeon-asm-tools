using System;
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
using System.Linq;

namespace VSRAD.Syntax.IntelliSense
{
    public interface INavigationTokenService
    {
        INavigationToken CreateToken(IDefinitionToken analysisToken, IDocument document);
        Task<NavigationTokenServiceResult> GetNavigationsAsync(SnapshotPoint point);
        void NavigateOrOpenNavigationList(IReadOnlyList<INavigationToken> navigations);
    }

    [Export(typeof(INavigationTokenService))]
    internal class NavigationTokenService : INavigationTokenService
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly IInstructionListManager _instructionListManager;

        [ImportingConstructor]
        public NavigationTokenService(IDocumentFactory documentFactory, IInstructionListManager instructionListManager)
        {
            _documentFactory = documentFactory;
            _instructionListManager = instructionListManager;
        }

        public INavigationToken CreateToken(IDefinitionToken analysisToken, IDocument document)
        {
            if (analysisToken == null) throw new ArgumentNullException(nameof(analysisToken));
            return new NavigationToken(analysisToken, document);
        }

        public async Task<NavigationTokenServiceResult> GetNavigationsAsync(SnapshotPoint point)
        {
            var document = _documentFactory.GetOrCreateDocument(point.Snapshot.TextBuffer);
            if (document == null) return null;

            var analysisResult = await document.DocumentAnalysis
                .GetAnalysisResultAsync(point.Snapshot)
                .ConfigureAwait(false);
            var analysisToken = analysisResult.GetToken(point);

            if (analysisToken == null) return null;
            var tokens = new List<INavigationToken>();

            switch (analysisToken)
            {
                case DefinitionToken definitionToken:
                    {
                        tokens.Add(CreateToken(definitionToken, document));
                        break;
                    }
                case ReferenceToken referenceToken:
                    {
                        var definition = referenceToken.Definition;
                        var textBuffer = definition.Span.Snapshot.TextBuffer;
                        var definitionDocument = _documentFactory.GetOrCreateDocument(textBuffer);

                        // if document is closed
                        if (definitionDocument == null) return null;

                        tokens.Add(CreateToken(definition, definitionDocument));
                        break;
                    }
                default:
                    {
                        if (analysisToken.Type != RadAsmTokenType.Instruction) return null;

                        var asmType = analysisResult.Snapshot.GetAsmType();
                        var instructionText = analysisToken.GetText();
                        var navigations = _instructionListManager.GetInstructionsByName(asmType, instructionText);

                        tokens.AddRange(navigations);
                        break;
                    }
            }

            return new NavigationTokenServiceResult(tokens, analysisToken);
        }

        public void NavigateOrOpenNavigationList(IReadOnlyList<INavigationToken> navigations)
        {
            if (navigations.Count == 1) navigations[0].Navigate();
            else if (navigations.Count > 1) NavigationList.UpdateNavigationList(navigations);
            else Error.ShowWarningMessage("Cannot navigate to the symbol under the cared");
        }
    }
}