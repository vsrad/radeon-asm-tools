using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Threading;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekResultSource : IPeekResultSource
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly NavigationToken _token;

        public PeekResultSource(IPeekResultFactory peekResultFactory, NavigationToken navigationToken)
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
            var tokenEnd = _token.GetEnd();
            var line = tokenEnd.GetContainingLine();
            var lineNumber = line.LineNumber;

            var startLineIndex = 0;
            var endLineIndex = line.End - line.Start;
            var idIndex = tokenEnd - line.Start;

            var displayInfo = new PeekResultDisplayInfo(
                label: _token.Path,
                labelTooltip: _token.Path,
                title: _token.Path,
                titleTooltip: _token.Path);

            return _peekResultFactory.Create(
                displayInfo,
                _token.Path,
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
