using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(CompletionSourceProvider))]
    [Order]
    internal class CompletionSourceProvider : ICompletionSourceProvider
    {
        private readonly IGlyphService _glyphService;
        private readonly ITextStructureNavigatorSelectorService _textStructureNavigatorSelector;
        private readonly InstructionListManager _instructionListManager;

        [ImportingConstructor]
        public CompletionSourceProvider(
            IGlyphService glyphService, 
            ITextStructureNavigatorSelectorService textStructureNavigatorSelectorService, 
            InstructionListManager instructionListManager)
        {
            _glyphService = glyphService;
            _textStructureNavigatorSelector = textStructureNavigatorSelectorService;
            _instructionListManager = instructionListManager;
        }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            var textStructureNavigator = _textStructureNavigatorSelector.GetTextStructureNavigator(textBuffer);
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new CompletionSource(_glyphService, textStructureNavigator, _instructionListManager));
        }
    }
}
