using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(CompletionSourceProvider))]
    internal class CompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly ITextStructureNavigatorSelectorService _textStructureNavigatorSelector;
        private readonly InstructionListManager _instructionListManager;
        private readonly OptionsProvider _optionsEventProvider;

        [ImportingConstructor]
        public CompletionSourceProvider(
            ITextStructureNavigatorSelectorService textStructureNavigatorSelectorService,
            OptionsProvider optionsEventProvider,
            InstructionListManager instructionListManager)
        {
            _textStructureNavigatorSelector = textStructureNavigatorSelectorService;
            _instructionListManager = instructionListManager;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView.TextBuffer == null)
                throw new ArgumentNullException(nameof(textView));

            var textStructureNavigator = _textStructureNavigatorSelector.GetTextStructureNavigator(textView.TextBuffer);
            return new BasicCompletionSource(textStructureNavigator, _optionsEventProvider);
        }
    }

    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(FunctionCompletionSourceProvider))]
    internal class FunctionCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly ITextStructureNavigatorSelectorService _textStructureNavigatorSelector;
        private readonly InstructionListManager _instructionListManager;
        private readonly OptionsProvider _optionsEventProvider;

        [ImportingConstructor]
        public FunctionCompletionSourceProvider(
            ITextStructureNavigatorSelectorService textStructureNavigatorSelectorService,
            OptionsProvider optionsEventProvider,
            InstructionListManager instructionListManager)
        {
            _textStructureNavigatorSelector = textStructureNavigatorSelectorService;
            _instructionListManager = instructionListManager;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView.TextBuffer == null)
                throw new ArgumentNullException(nameof(textView));

            var textStructureNavigator = _textStructureNavigatorSelector.GetTextStructureNavigator(textView.TextBuffer);
            return new FunctionCompletionSource(textStructureNavigator, _optionsEventProvider, textView.GetParserManager());
        }
    }

    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(InstructionCompletionSourceProvider))]
    internal class InstructionCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly ITextStructureNavigatorSelectorService _textStructureNavigatorSelector;
        private readonly InstructionListManager _instructionListManager;
        private readonly OptionsProvider _optionsEventProvider;

        [ImportingConstructor]
        public InstructionCompletionSourceProvider(
            ITextStructureNavigatorSelectorService textStructureNavigatorSelectorService,
            OptionsProvider optionsEventProvider,
            InstructionListManager instructionListManager)
        {
            _textStructureNavigatorSelector = textStructureNavigatorSelectorService;
            _instructionListManager = instructionListManager;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView.TextBuffer == null)
                throw new ArgumentNullException(nameof(textView));

            var textStructureNavigator = _textStructureNavigatorSelector.GetTextStructureNavigator(textView.TextBuffer);
            return new InstructionCompletionSource(textStructureNavigator, _instructionListManager, _optionsEventProvider);
        }
    }
}
