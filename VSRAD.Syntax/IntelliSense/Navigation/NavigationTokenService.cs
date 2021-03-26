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

namespace VSRAD.Syntax.IntelliSense
{
    public interface INavigationTokenService
    {
        INavigationToken CreateToken(IAnalysisToken analysisToken);
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

        public INavigationToken CreateToken(IAnalysisToken analysisToken)
        {
            var document = _documentFactory.GetOrCreateDocument(analysisToken.Snapshot.TextBuffer);
            return CreateToken(analysisToken, document);
        }

        private static INavigationToken CreateToken(IAnalysisToken analysisToken, IDocument document)
        {
            var navigateAction = GetNavigateAction(analysisToken, document);
            return new NavigationToken(analysisToken, document.Path, navigateAction);
        }

        private static Action GetNavigateAction(IAnalysisToken analysisToken, IDocument document) =>
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
                        var definitionDocument = _documentFactory.GetOrCreateDocument(definition.Snapshot.TextBuffer);
                        tokens.Add(CreateToken(definition, definitionDocument));
                        break;
                    }
                default:
                    {
                        if (analysisToken.Type != RadAsmTokenType.Instruction) return null;

                        var asmType = analysisToken.Snapshot.GetAsmType();
                        var instructions = _instructionListManager.GetSelectedSetInstructions(asmType);
                        var instructionText = analysisToken.Text;

                        var navigations = instructions
                            .Where(i => i.Text == instructionText)
                            .SelectMany(i => i.Navigations);
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