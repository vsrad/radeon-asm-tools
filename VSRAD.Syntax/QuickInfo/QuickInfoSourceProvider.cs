﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Peek.DefinitionService;

namespace VSRAD.Syntax.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(QuickInfoSourceProvider))]
    [Order]
    internal class QuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        private readonly DefinitionService _definitionService;

        [ImportingConstructor]
        public QuickInfoSourceProvider(DefinitionService definitionService)
        {
            _definitionService = definitionService;
        }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new QuickInfoSource(textBuffer, _definitionService));
        }
    }
}