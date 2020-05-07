using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
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
        private readonly InstructionListManager _instructionListManager;
        private readonly OptionsProvider _optionsEventProvider;

        [ImportingConstructor]
        public CompletionSourceProvider(
            OptionsProvider optionsEventProvider,
            InstructionListManager instructionListManager)
        {
            _instructionListManager = instructionListManager;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView.TextBuffer == null)
                throw new ArgumentNullException(nameof(textView));

            return new BasicCompletionSource(_optionsEventProvider, textView.GetParserManager());
        }
    }

    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(FunctionCompletionSourceProvider))]
    internal class FunctionCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly InstructionListManager _instructionListManager;
        private readonly OptionsProvider _optionsEventProvider;

        [ImportingConstructor]
        public FunctionCompletionSourceProvider(
            OptionsProvider optionsEventProvider,
            InstructionListManager instructionListManager)
        {
            _instructionListManager = instructionListManager;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView.TextBuffer == null)
                throw new ArgumentNullException(nameof(textView));

            return new FunctionCompletionSource(_optionsEventProvider, textView.GetParserManager());
        }
    }

    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(InstructionCompletionSourceProvider))]
    internal class InstructionCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly InstructionListManager _instructionListManager;
        private readonly OptionsProvider _optionsEventProvider;

        [ImportingConstructor]
        public InstructionCompletionSourceProvider(
            OptionsProvider optionsEventProvider,
            InstructionListManager instructionListManager)
        {
            _instructionListManager = instructionListManager;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView.TextBuffer == null)
                throw new ArgumentNullException(nameof(textView));

            return new InstructionCompletionSource(_instructionListManager, _optionsEventProvider, textView.GetParserManager());
        }
    }
}
