using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class ScopedCompletionProvider : RadCompletionProvider
    {
        private static readonly ImageElement FunctionIcon = GetImageElement(KnownImageIds.Method);
        private static readonly ImageElement LabelIcon = GetImageElement(KnownImageIds.Label);
        private static readonly ImageElement PreprocessorMacroIcon = GetImageElement(KnownImageIds.MacroPublic);
        private static readonly ImageElement GlobalVariableIcon = GetImageElement(KnownImageIds.GlobalVariable);
        private static readonly ImageElement LocalVariableIcon = GetImageElement(KnownImageIds.LocalVariable);
        private static readonly ImageElement ArgumentIcon = GetImageElement(KnownImageIds.Parameter);

        private readonly IIntelliSenseService _intelliSenseService;

        private bool _autocompleteFunctions;
        private bool _autocompleteLabels;
        private bool _autocompleteVariables;
        private bool _autocompletePreprocessorMacros;

        public ScopedCompletionProvider(OptionsProvider optionsProvider, IIntelliSenseService intelliSenseService)
            : base(optionsProvider)
        {
            _intelliSenseService = intelliSenseService;
            _autocompleteFunctions = optionsProvider.AutocompleteFunctions;
            _autocompleteLabels = optionsProvider.AutocompleteLabels;
            _autocompleteVariables = optionsProvider.AutocompleteVariables;
            _autocompletePreprocessorMacros = optionsProvider.AutocompletePreprocessorMacros;
        }

        public override void DisplayOptionsUpdated(OptionsProvider sender)
        {
            _autocompleteFunctions = sender.AutocompleteFunctions;
            _autocompleteLabels = sender.AutocompleteLabels;
            _autocompleteVariables = sender.AutocompleteVariables;
            _autocompletePreprocessorMacros = sender.AutocompletePreprocessorMacros;
        }

        public override async Task<RadCompletionContext> GetContextAsync(IDocument document, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken)
        {
            if (!_autocompleteFunctions && !_autocompleteLabels && !_autocompleteVariables && !_autocompletePreprocessorMacros)
                return RadCompletionContext.Empty;

            var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(triggerLocation.Snapshot);
            var completions = new List<RadCompletionItem>();
            for (var block = analysisResult.GetBlock(triggerLocation); block != null; block = block.Parent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var token in block.Tokens)
                {
                    switch (token.Type)
                    {
                        case RadAsmTokenType.FunctionName:
                            if (_autocompleteFunctions)
                                completions.Add(new RadCompletionItem(_intelliSenseService.GetIntelliSenseInfo(document, token), FunctionIcon));
                            break;
                        case RadAsmTokenType.Label:
                            if (_autocompleteLabels)
                                completions.Add(new RadCompletionItem(_intelliSenseService.GetIntelliSenseInfo(document, token), LabelIcon));
                            break;
                        case RadAsmTokenType.PreprocessorMacro:
                            if (_autocompletePreprocessorMacros)
                                completions.Add(new RadCompletionItem(_intelliSenseService.GetIntelliSenseInfo(document, token), PreprocessorMacroIcon));
                            break;
                        case RadAsmTokenType.GlobalVariable:
                            if (_autocompleteVariables)
                                completions.Add(new RadCompletionItem(_intelliSenseService.GetIntelliSenseInfo(document, token), GlobalVariableIcon));
                            break;
                        case RadAsmTokenType.LocalVariable:
                            if (_autocompleteVariables)
                                completions.Add(new RadCompletionItem(_intelliSenseService.GetIntelliSenseInfo(document, token), LocalVariableIcon));
                            break;
                        case RadAsmTokenType.FunctionParameter:
                            if (_autocompleteVariables)
                                completions.Add(new RadCompletionItem(_intelliSenseService.GetIntelliSenseInfo(document, token), ArgumentIcon));
                            break;
                    }
                }
            }
            return new RadCompletionContext(completions);
        }
    }
}
