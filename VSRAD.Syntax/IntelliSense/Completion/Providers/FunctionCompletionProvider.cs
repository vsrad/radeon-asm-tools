using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class FunctionCompletionProvider : CompletionProvider
    {
        private static readonly ImageElement FunctionIcon = GetImageElement(KnownImageIds.Method);
        private bool _autocompleteFunctions;

        public FunctionCompletionProvider(OptionsProvider optionsProvider)
            : base(optionsProvider)
        {
            _autocompleteFunctions = optionsProvider.AutocompleteFunctions;
        }

        public override void DisplayOptionsUpdated(OptionsProvider sender) =>
            _autocompleteFunctions = sender.AutocompleteFunctions;

        public override Task<CompletionContext> GetContextAsync(DocumentAnalysis documentAnalysis, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan)
        {
            if (!_autocompleteFunctions)
                return Task.FromResult(CompletionContext.Empty);

            var completionList = documentAnalysis
                    .LastParserResult
                    .GetFunctions()
                    .Select(f => new NavigationToken(f.Name, triggerLocation.Snapshot))
                    .Select(n => new CompletionItem(n.GetText(), FunctionIcon, n))
                    .ToList();

            return Task.FromResult(new CompletionContext(completionList));
        }
    }
}
