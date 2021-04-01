using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Threading;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekResultSource : IPeekResultSource
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly INavigationToken _token;

        public PeekResultSource(IPeekResultFactory peekResultFactory, INavigationToken navigationToken)
        {
            _peekResultFactory = peekResultFactory;
            _token = navigationToken;
        }

        public void FindResults(string relationshipName, IPeekResultCollection resultCollection, CancellationToken cancellationToken, IFindPeekResultsCallback callback)
        {
            if (resultCollection == null)
                throw new ArgumentNullException(nameof(resultCollection));

            if (!string.Equals(relationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase))
                return;

            resultCollection.Add(CreateResult());
        }

        private IDocumentPeekResult CreateResult()
        {
            var tokenLine = _token.GetLine();
            var lineNumber = tokenLine.LineNumber;

            const int startLineIndex = 0;
            var endLineIndex = tokenLine.LineEnd;
            var idIndex = _token.GetStart() - tokenLine.LineStart;
            var path = _token.Document.Path;

            var displayInfo = new PeekResultDisplayInfo(
                label: path,
                labelTooltip: path,
                title: path,
                titleTooltip: path);

            return _peekResultFactory.Create(
                displayInfo,
                path,
                lineNumber,
                startLineIndex,
                lineNumber,
                endLineIndex,
                idLine: lineNumber,
                idIndex: idIndex
            );
        }
    }
}
