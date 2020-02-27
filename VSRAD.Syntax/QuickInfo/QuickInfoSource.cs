using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser.Blocks;
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
                var dataElement = GetNavigationTokenContainerElement(textView, navigationToken);
                if (dataElement == null)
                    return Task.FromResult<QuickInfoItem>(null);

                var applicableToSpan = currentSnapshot.CreateTrackingSpan(extent.Span.Start, navigationToken.TokenName.Length, SpanTrackingMode.EdgeInclusive);
                return Task.FromResult(new QuickInfoItem(applicableToSpan, dataElement));
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        private static ContainerElement GetNavigationTokenContainerElement(IWpfTextView textView, IBaseToken token)
        {
            if (token == null)
                return null;
            switch (token.TokenType)
            {
                case TokenType.Argument:
                    return GetBasicContainerElement("function argument", token.TokenName, classificationTypeName: SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments);
                case TokenType.Function:
                    var functionBlock = textView.GetFunctionBlockByName(token.TokenName);
                    return GetFunctionContainerElement(functionBlock);
                case TokenType.Variable:
                    return GetBasicContainerElement("local variable", token.TokenName, ((IDescriptionToken)token).Description, SyntaxHighlighter.PredefinedClassificationTypeNames.Keywords);
                default:
                    return null;
            }
        }

        private static ContainerElement GetBasicContainerElement(string typeName, ClassifiedTextElement nameElement, string description = "")
        {
            var tokenElement = new ContainerElement(
                ContainerElementStyle.Wrapped,
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, $"({typeName}) ")
                ),
                nameElement
                );

            if (string.IsNullOrEmpty(description))
                return tokenElement;

            return new ContainerElement(
                    ContainerElementStyle.Stacked,
                    tokenElement,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, "")
                    ),
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, description)
                    )
                );
        }

        private static ContainerElement GetBasicContainerElement(string typeName, string name, string description = "", string classificationTypeName = PredefinedClassificationTypeNames.Identifier)
        {
            var nameElement = new ClassifiedTextElement(
                new ClassifiedTextRun(classificationTypeName, name)
                );

            return GetBasicContainerElement(typeName, nameElement, description);
        }

        private static ContainerElement GetFunctionContainerElement(FunctionBlock functionBlock)
        {
            if (functionBlock == null) 
                return null;

            var funKeyword = functionBlock.BlockSpan.Snapshot.IsRadeonAsm2ContentType() ? Constants.asm2FunctionKeyword : Constants.asm1FunctionKeyword;
            var textRuns = new List<ClassifiedTextRun>()
            {
                new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Keywords, $"{funKeyword} "),
                new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Functions, $"{functionBlock.FunctionToken.TokenName} ")
            };
            if (functionBlock.BlockSpan.Snapshot.IsRadeonAsm2ContentType())
                textRuns.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, "( "));

            var argTokenNames = functionBlock.GetArgumentTokens().Select(token => token.TokenName).ToList();
            for (var i = 0; i < argTokenNames.Count; i++)
            {
                var name = (i != argTokenNames.Count - 1) ? $"{argTokenNames[i]}, " : argTokenNames[i];
                textRuns.Add(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments, name));
            }
            if (functionBlock.BlockSpan.Snapshot.IsRadeonAsm2ContentType())
                textRuns.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, " )"));

            var functionName = new ClassifiedTextElement(textRuns);
            return GetBasicContainerElement("function", functionName, ((IDescriptionToken)functionBlock.FunctionToken).Description);
        }

        public void Dispose()
        {
        }
    }
}
