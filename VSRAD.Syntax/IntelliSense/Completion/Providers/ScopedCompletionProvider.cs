using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class ScopedCompletionProvider : CompletionProvider
    {
        private static readonly ImageElement LabelIcon = GetImageElement(KnownImageIds.Label);
        private static readonly ImageElement GlobalVariableIcon = GetImageElement(KnownImageIds.GlobalVariable);
        private static readonly ImageElement LocalVariableIcon = GetImageElement(KnownImageIds.LocalVariable);
        private static readonly ImageElement ArgumentIcon = GetImageElement(KnownImageIds.Parameter);

        private bool _autocompleteLabels;
        private bool _autocompleteVariables;

        public ScopedCompletionProvider(OptionsProvider optionsProvider)
            : base(optionsProvider)
        {
            _autocompleteLabels = optionsProvider.AutocompleteLabels;
            _autocompleteVariables = optionsProvider.AutocompleteVariables;
        }

        public override void DisplayOptionsUpdated(OptionsProvider sender)
        {
            _autocompleteLabels = sender.AutocompleteLabels;
            _autocompleteVariables = sender.AutocompleteVariables;
        }

        public override Task<CompletionContext> GetContextAsync(DocumentAnalysis documentAnalysis, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan)
        {
            var completionList = Enumerable.Empty<CompletionItem>();

            if (_autocompleteLabels)
                completionList = completionList
                    .Concat(GetScopedCompletions(documentAnalysis, triggerLocation, RadAsmTokenType.Label, LabelIcon));

            if (_autocompleteVariables)
                completionList = completionList
                    .Concat(GetScopedCompletions(documentAnalysis, triggerLocation, RadAsmTokenType.GlobalVariable, GlobalVariableIcon))
                    .Concat(GetScopedCompletions(documentAnalysis, triggerLocation, RadAsmTokenType.LocalVariable, LocalVariableIcon))
                    .Concat(GetScopedCompletions(documentAnalysis, triggerLocation, RadAsmTokenType.FunctionParameter, ArgumentIcon));

            return Task.FromResult(new CompletionContext(completionList.ToList()));
        }

        private static IEnumerable<CompletionItem> GetScopedCompletions(DocumentAnalysis documentAnalysis, SnapshotPoint triggerPoint, RadAsmTokenType type, ImageElement icon)
        {
            var currentBlock = documentAnalysis
                .LastParserResult
                .GetBlockBy(triggerPoint);

            return currentBlock
                .GetScopedTokens(type)
                .Select(a => new NavigationToken(a, triggerPoint.Snapshot))
                .Select(n => new CompletionItem(n.GetText(), icon, n));
        }
    }
}
