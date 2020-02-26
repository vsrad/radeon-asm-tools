using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Operations;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.Peek.DefinitionService;

namespace VSRAD.Syntax.QuickInfo
{
    internal class QuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly DefinitionService _definitionService;
        private readonly ITextBuffer _textBuffer;

        public QuickInfoSource(ITextBuffer textBuffer, DefinitionService definitionService)
        {
            _textBuffer = textBuffer;
            _definitionService = definitionService;
        }

        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
                return Task.FromResult<QuickInfoItem>(null);

            var textView = _definitionService.GetWpfTextView();
            if (textView.TextBuffer.CurrentSnapshot != _textBuffer.CurrentSnapshot)
                return Task.FromResult<QuickInfoItem>(null);

            var currentSnapshot = _textBuffer.CurrentSnapshot;
            var extent = _definitionService.GetTextExtent(_textBuffer, triggerPoint.Value);

            var navigationToken = _definitionService.GetNaviationItem(textView, extent);
            if (navigationToken != null)
            {
                var dataElement = GetNavigationTokenContainerElement(navigationToken);
                if (dataElement == null)
                    return Task.FromResult<QuickInfoItem>(null);

                var applicableToSpan = currentSnapshot.CreateTrackingSpan(extent.Span.Start, navigationToken.TokenName.Length, SpanTrackingMode.EdgeInclusive);
                return Task.FromResult(new QuickInfoItem(applicableToSpan, dataElement));
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        private static ContainerElement GetNavigationTokenContainerElement(IBaseToken token)
        {
            switch (token.TokenType)
            {
                case TokenType.Argument:
                    return GetContainerElement("function argument", "", token.TokenName, PredefinedClassificationTypeNames.SymbolDefinition);
                case TokenType.Function:
                    return GetContainerElement("function", "", token.TokenName, SyntaxHighlighter.PredefinedClassificationTypeNames.Functions);
                case TokenType.Variable:
                    return GetContainerElement("local variable", "", token.TokenName, PredefinedClassificationTypeNames.SymbolDefinition);
                default:
                    return null;
            }
        }

        private static ContainerElement GetContainerElement(string typeName, string description, string name, string classificationName)
        {
            var tokenElement = new ContainerElement(
                ContainerElementStyle.Wrapped,
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, $"({typeName}) "),
                    new ClassifiedTextRun(classificationName, name)
                    )
                );

            return new ContainerElement(
                    ContainerElementStyle.Stacked,
                    tokenElement,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, description)
                    )
                );
        }

        public void Dispose()
        {
        }
    }
}
