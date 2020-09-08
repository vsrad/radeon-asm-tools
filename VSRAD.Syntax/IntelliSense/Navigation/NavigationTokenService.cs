using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.IntelliSense.Navigation;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Options.Instructions;
using System;
using VSRAD.Syntax.IntelliSense.Navigation.NavigationList;

namespace VSRAD.Syntax.IntelliSense
{
    public interface INavigationTokenService
    {
        NavigationToken CreateToken(AnalysisToken analysisToken, string path);
        Task<NavigationTokenServiceResult> GetNavigationsAsync(SnapshotPoint point);
        void NavigateOrOpenNavigationList(IReadOnlyList<NavigationToken> navigations);
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

        public NavigationToken CreateToken(AnalysisToken analysisToken, string path)
        {
            var document = _documentFactory.GetOrCreateDocument(analysisToken.Snapshot.TextBuffer);
            var navigate = document != null
                ? new Action(() => document.NavigateToPosition(analysisToken.GetStart()))
                : null;
            return new NavigationToken(analysisToken, path, navigate);
        }

        public async Task<NavigationTokenServiceResult> GetNavigationsAsync(SnapshotPoint point)
        {
            var document = _documentFactory.GetOrCreateDocument(point.Snapshot.TextBuffer);
            if (document == null) return NavigationTokenServiceResult.Empty;

            var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(point.Snapshot);
            var analysisToken = analysisResult.GetToken(point);

            if (analysisToken == null) return NavigationTokenServiceResult.Empty;
            var tokens = new List<NavigationToken>();

            if (analysisToken is DefinitionToken definitionToken)
            {
                tokens.Add(CreateToken(definitionToken, document.Path));
            }
            else if (analysisToken is ReferenceToken referenceToken)
            {
                var definition = referenceToken.Definition;
                var definitionDocument = _documentFactory.GetOrCreateDocument(definition.Snapshot.TextBuffer);
                tokens.Add(CreateToken(definition, definitionDocument.Path));
            }
            else
            {
                if (analysisToken.Type == RadAsmTokenType.Instruction)
                {
                    var asmType = analysisToken.Snapshot.GetAsmType();
                    var instructions = _instructionListManager.GetInstructions(asmType);
                    var instructionText = analysisToken.GetText();
                    foreach (var i in instructions)
                    {
                        if (i.Text == instructionText)
                            tokens.Add(i.Navigation);
                    }
                }
                else
                {
                    return NavigationTokenServiceResult.Empty;
                }
            }

            return new NavigationTokenServiceResult(tokens, analysisToken);
        }

        public void NavigateOrOpenNavigationList(IReadOnlyList<NavigationToken> navigations)
        {
            if (navigations.Count == 1) navigations[0].Navigate();
            else if (navigations.Count > 1) NavigationList.UpdateNavigationList(navigations);
            else Error.ShowWarningMessage("Cannot navigate to the symbol under the cared");
        }
    }
}