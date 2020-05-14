using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class FunctionCompletionSource : BasicCompletionSource
    {
        private static readonly ImageElement Icon = GetImageElement(KnownImageIds.Method);
        private bool _autocompleteFunctions;

        public FunctionCompletionSource(
            OptionsProvider optionsProvider, 
            IParserManager parserManager) : base(optionsProvider, parserManager)
        {
            DisplayOptionsUpdated(optionsProvider);
        }

        public override Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (!_autocompleteFunctions)
                return Task.FromResult(CompletionContext.Empty);

            var parser = ParserManager.ActualParser;
            if (parser == null)
                return Task.FromResult(CompletionContext.Empty);

            var triggerText = triggerLocation
                .GetExtent()
                .Span.GetText();

            var completions = parser
                .GetFunctionTokens()
                .Where(t => t.TokenName.Contains(triggerText))
                .Select(t => new CompletionItem(t.TokenName, this, Icon))
                .OrderBy(i => i.DisplayText)
                .ToImmutableArray();

            return Task.FromResult(new CompletionContext(completions));
        }

        public override Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            var parser = ParserManager.ActualParser;
            if (parser == null)
                return Task.FromResult<object>(null);

            var fb = parser.GetFunction(item.DisplayText);
            if (fb == null)
                return Task.FromResult<object>(null);

            return Task.FromResult(IntellisenseTokenDescription.GetColorizedDescription(fb.FunctionToken));
        }

        protected override void DisplayOptionsUpdated(OptionsProvider sender) =>
            _autocompleteFunctions = sender.AutocompleteFunctions;
    }
}
