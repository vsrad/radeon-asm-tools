﻿using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.IntelliSense.Completion.Providers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(CompletionSourceProvider))]
    internal class CompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly IIntellisenseDescriptionBuilder _descriptionBuilder;
        private readonly IDocumentFactory _documentFactory;
        private readonly IReadOnlyList<RadCompletionProvider> _providers;

        [ImportingConstructor]
        public CompletionSourceProvider(IInstructionListManager instructionListManager,
            IIntellisenseDescriptionBuilder descriptionBuilder,
            IDocumentFactory documentFactory, 
            INavigationTokenService navigationTokenService)
        {
            _descriptionBuilder = descriptionBuilder;
            _documentFactory = documentFactory;
            
            var optionProvider = GeneralOptionProvider.Instance;
            _providers = new List<RadCompletionProvider>()
            {
                new InstructionCompletionProvider(optionProvider, instructionListManager),
                new FunctionCompletionProvider(optionProvider, navigationTokenService),
                new ScopedCompletionProvider(optionProvider, navigationTokenService),
            };
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView == null)
                throw new ArgumentNullException(nameof(textView));

            var document = _documentFactory.GetOrCreateDocument(textView.TextBuffer);
            if (document == null) return null;

            return new CompletionSource(document, _descriptionBuilder, _providers);
        }
    }
}
