using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.PatternMatching;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(IAsyncCompletionItemManagerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(ItemManagerProvider))]
    internal class ItemManagerProvider : IAsyncCompletionItemManagerProvider
    {
        private readonly ItemManager _instance;

        [ImportingConstructor]
        public ItemManagerProvider(IPatternMatcherFactory patternMatcherFactory, IDocumentFactory documentFactory, OptionsProvider optionsProvider)
        {
            _instance = new ItemManager(patternMatcherFactory, documentFactory, optionsProvider);
        }

        public IAsyncCompletionItemManager GetOrCreate(ITextView textView) => _instance;
    }
}
