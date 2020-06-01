using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(ScopeTokenCompletionSourceProvider))]
    internal class ScopeTokenCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        private readonly OptionsProvider _optionsEventProvider;

        [ImportingConstructor]
        public ScopeTokenCompletionSourceProvider(
            OptionsProvider optionsEventProvider,
            DocumentAnalysisProvoder documentAnalysisProvoder)
        {
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView == null)
                throw new ArgumentNullException(nameof(textView));

            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(textView.TextBuffer);
            return new ScopeTokenCompletionSource(_optionsEventProvider, documentAnalysis);
        }
    }

    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(FunctionCompletionSourceProvider))]
    internal class FunctionCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        private readonly OptionsProvider _optionsEventProvider;

        [ImportingConstructor]
        public FunctionCompletionSourceProvider(
            OptionsProvider optionsEventProvider,
            DocumentAnalysisProvoder documentAnalysisProvoder)
        {
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView == null)
                throw new ArgumentNullException(nameof(textView));

            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(textView.TextBuffer);
            return new FunctionCompletionSource(_optionsEventProvider, documentAnalysis);
        }
    }

    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(InstructionCompletionSourceProvider))]
    internal class InstructionCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly InstructionListManager _instructionListManager;
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        private readonly OptionsProvider _optionsEventProvider;

        [ImportingConstructor]
        public InstructionCompletionSourceProvider(
            OptionsProvider optionsEventProvider,
            DocumentAnalysisProvoder documentAnalysisProvoder,
            InstructionListManager instructionListManager)
        {
            _instructionListManager = instructionListManager;
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView == null)
                throw new ArgumentNullException(nameof(textView));

            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(textView.TextBuffer);
            return new InstructionCompletionSource(_instructionListManager, _optionsEventProvider, documentAnalysis);
        }
    }
}
