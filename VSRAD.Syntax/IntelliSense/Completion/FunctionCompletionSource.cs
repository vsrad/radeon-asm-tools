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
using VSRAD.Syntax.Parser.Blocks;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class FunctionCompletionSource : BasicCompletionSource
    {
        private static readonly ImageElement Icon = GetImageElement(KnownImageIds.Method);
        private bool _autocompleteFunctions;

        public FunctionCompletionSource(
            OptionsProvider optionsProvider,
            DocumentAnalysis documentAnalysis) : base(optionsProvider, documentAnalysis)
        {
            DisplayOptionsUpdated(optionsProvider);
        }

        public override Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (!_autocompleteFunctions)
                return Task.FromResult<CompletionContext>(null);

            var triggerText = triggerLocation
                .GetExtent()
                .Span.GetText();

            var completions = DocumentAnalysis
                .LastParserResult
                .GetFunctions()
                .Where(t => t.Name.TrackingToken.GetText(triggerLocation.Snapshot).Contains(triggerText))
                .Select(t => new CompletionItem(t.Name.TrackingToken.GetText(triggerLocation.Snapshot), this, Icon))
                .OrderBy(i => i.DisplayText)
                .ToImmutableArray();

            return completions.Any()
                ? Task.FromResult(new CompletionContext(completions))
                : Task.FromResult<CompletionContext>(null);
        }

        public override Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            var fb = GetFunction(item.DisplayText, DocumentAnalysis.CurrentSnapshot);
            if (fb == null)
                return Task.FromResult<object>(null);

            return Task.FromResult(IntellisenseTokenDescription.GetColorizedTokenDescription(DocumentAnalysis, fb.Name));
        }

        protected override void DisplayOptionsUpdated(OptionsProvider sender) =>
            _autocompleteFunctions = sender.AutocompleteFunctions;

        private FunctionBlock GetFunction(string name, ITextSnapshot version) =>
            DocumentAnalysis.LastParserResult.GetFunctions().Where(f => f.Name.TrackingToken.GetText(version) == name).FirstOrDefault();
    }
}
