using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal sealed class BuiltinCompletionProvider : RadCompletionProvider
    {
        private static readonly ImageElement FunctionIcon = GetImageElement(KnownImageIds.Method);

        private readonly OptionsProvider _optionsProvider;
        private readonly IBuiltinInfoProvider _builtinInfoProvider;

        public BuiltinCompletionProvider(OptionsProvider optionsProvider, IBuiltinInfoProvider builtinInfoProvider)
            : base(optionsProvider)
        {
            _optionsProvider = optionsProvider;
            _builtinInfoProvider = builtinInfoProvider;
        }

        public override void DisplayOptionsUpdated(OptionsProvider sender)
        {
        }

        public override Task<RadCompletionContext> GetContextAsync(IDocument document, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken)
        {
            if (!_optionsProvider.AutocompleteBuiltins)
                return Task.FromResult(RadCompletionContext.Empty);

            var asmType = document.CurrentSnapshot.GetAsmType();
            var completionItems = _builtinInfoProvider.GetBuiltins(asmType)
                .Select(b => new RadCompletionItem(new IntelliSenseInfo(asmType, b.Name, Core.Tokens.RadAsmTokenType.BuiltinFunction, null, Array.Empty<NavigationToken>(), null, b), FunctionIcon))
                .ToList();
            return Task.FromResult(new RadCompletionContext(completionItems));
        }
    }
}
