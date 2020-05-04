using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser.Tokens;
using VsCompletion = Microsoft.VisualStudio.Language.Intellisense.Completion;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class CompletionSource : ICompletionSource
    {
        private readonly ITextStructureNavigator _textStructureNavigator;
        private readonly IGlyphService _glyphService;
        private IEnumerable<VsCompletion> _instructionCompletions;
        private bool _isDisposed;

        private bool _autocompleteInstructions;
        private bool _autocompleteFunctions;
        private bool _autocompleteLabels;
        private bool _autocompleteVariables;

        public CompletionSource(
            IGlyphService glyphService,
            ITextStructureNavigator textStructureNavigator,
            InstructionListManager instructionListManager,
            OptionsEventProvider optionsProvider)
        {
            _textStructureNavigator = textStructureNavigator;
            _glyphService = glyphService;

            instructionListManager.InstructionUpdated += InstructionUpdated;
            optionsProvider.OptionsUpdated += DisplayOptionsUpdated;

            InstructionUpdated(instructionListManager.InstructionList);
            DisplayOptionsUpdated(optionsProvider);
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            try
            {
                if (_autocompleteInstructions)
                    AddCompletionSet(completionSets, "RadInstructions", FindTokenSpanAtPosition(session), _instructionCompletions);
                if (_autocompleteFunctions)
                    AddCompletionSet(completionSets, "RadFunctions", FindTokenSpanAtPosition(session), GetScopedCompletions(session, TokenType.Function));
                if (_autocompleteLabels)
                    AddCompletionSet(completionSets, "RadLables", FindTokenSpanAtPosition(session), GetScopedCompletions(session, TokenType.Label));
                if (_autocompleteVariables)
                {
                    AddCompletionSet(completionSets, "RadArguments", FindTokenSpanAtPosition(session), GetScopedCompletions(session, TokenType.Argument));
                    AddCompletionSet(completionSets, "RadGlobalVariable", FindTokenSpanAtPosition(session), GetScopedCompletions(session, TokenType.GlobalVariable));
                    AddCompletionSet(completionSets, "RadLocalVariable", FindTokenSpanAtPosition(session), GetScopedCompletions(session, TokenType.LocalVariable));
                }
            }
            catch (Exception e)
            {
                Error.LogError(e, "CompletionSource");
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                GC.SuppressFinalize(this);
                _isDisposed = true;
            }
        }

        private static void AddCompletionSet(IList<CompletionSet> completionSets, string displayName, ITrackingSpan applicableTo, IEnumerable<VsCompletion> completions) =>
            completionSets.Add(new CompletionSet(displayName, displayName, applicableTo, completions, null));

        private ITrackingSpan FindTokenSpanAtPosition(ICompletionSession session)
        {
            var currentPoint = session.TextView.Caret.Position.BufferPosition;
            // show set after only two characters
            if (currentPoint > 2)
                currentPoint -= 2;

            var extent = _textStructureNavigator.GetExtentOfWord(currentPoint);
            return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
        }

        private IEnumerable<VsCompletion> GetScopedCompletions(ICompletionSession session, TokenType tokenType)
        {
            var scopedCompletions = Enumerable.Empty<VsCompletion>();

            var parserManager = session.TextView.GetParserManager();
            var parser = parserManager?.ActualParser;
            if (parser == null)
                return scopedCompletions;

            scopedCompletions = parser
                .GetScopedTokens(session.TextView.Caret.Position.BufferPosition, tokenType)
                .OrderBy(t => t.TokenName)
                .Select(t => InitializeCompletion(t.TokenName, tokenType.GetName(), tokenType.GetGlyphGroup()));

            return scopedCompletions;
        }

        private void InstructionUpdated(IReadOnlyList<string> instructions)
        {
            _instructionCompletions = instructions
                .OrderBy(i => i)
                .Select(i => InitializeCompletion(i, "instruction", StandardGlyphGroup.GlyphGroupField));
        }

        private void DisplayOptionsUpdated(OptionsEventProvider options)
        {
            _autocompleteInstructions = options.AutocompleteInstructions;
            _autocompleteFunctions = options.AutocompleteFunctions;
            _autocompleteLabels = options.AutocompleteLabels;
            _autocompleteVariables = options.AutocompleteVariables;
        }

        private VsCompletion InitializeCompletion(string text, string type, StandardGlyphGroup group) =>
            new VsCompletion(text, text, $"({type}) {text}", _glyphService.GetGlyph(group, StandardGlyphItem.GlyphItemPublic), type);
    }
}
