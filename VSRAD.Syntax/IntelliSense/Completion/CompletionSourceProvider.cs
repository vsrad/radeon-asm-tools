using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Syntax.IntelliSense.Completion.Providers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(CompletionSourceProvider))]
    internal class CompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;
        private readonly OptionsProvider _optionsEventProvider;
        private readonly IIntellisenseDescriptionBuilder _descriptionBuilder;
        private readonly IReadOnlyList<CompletionProvider> _providers;

        [ImportingConstructor]
        public CompletionSourceProvider(
            OptionsProvider optionsEventProvider,
            InstructionListManager instructionListManager,
            DocumentAnalysisProvoder documentAnalysisProvoder,
            IIntellisenseDescriptionBuilder descriptionBuilder)
        {
            _documentAnalysisProvoder = documentAnalysisProvoder;
            _optionsEventProvider = optionsEventProvider;
            _descriptionBuilder = descriptionBuilder;

            _providers = new List<CompletionProvider>()
            {
                new InstructionCompletionProvider(instructionListManager, optionsEventProvider),
                new FunctionCompletionProvider(optionsEventProvider),
                new ScopedCompletionProvider(optionsEventProvider)
            };
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView == null)
                throw new ArgumentNullException(nameof(textView));

            var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(textView.TextBuffer);
            return new CompletionSource(documentAnalysis, _descriptionBuilder, _providers);
        }
    }
}
