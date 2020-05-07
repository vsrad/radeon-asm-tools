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
                var completions = Enumerable.Empty<VsCompletion>();
                if (_autocompleteInstructions)
                    completions = completions.Concat(_instructionCompletions);
                if (_autocompleteFunctions)
                    completions = completions.Concat(GetScopedCompletions(session, TokenType.Function));
                if (_autocompleteLabels)
                    completions = completions.Concat(GetScopedCompletions(session, TokenType.Label));
                if (_autocompleteVariables)
                    completions = completions
                        .Concat(GetScopedCompletions(session, TokenType.Argument))
                        .Concat(GetScopedCompletions(session, TokenType.GlobalVariable))
                        .Concat(GetScopedCompletions(session, TokenType.LocalVariable));

                if (completions.Any())
                    completionSets.Add(new CompletionSet(Constants.CompletionSetName, Constants.CompletionSetName, FindTokenSpanAtPosition(session), completions, null));
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
                .Select(t => InitializeCompletion(t.TokenName, t.TokenName, GetFullName(t), GetTokenDescription(t), tokenType.GetName(), tokenType.GetGlyphGroup()));

            return scopedCompletions;
        }

        private void InstructionUpdated(IReadOnlyList<string> instructions)
        {
            _instructionCompletions = instructions
                .OrderBy(i => i)
                .Select(i => InitializeCompletion(i, i, i, "", "instruction", StandardGlyphGroup.GlyphGroupField));
        }

        private void DisplayOptionsUpdated(OptionsEventProvider options)
        {
            _autocompleteInstructions = options.AutocompleteInstructions;
            _autocompleteFunctions = options.AutocompleteFunctions;
            _autocompleteLabels = options.AutocompleteLabels;
            _autocompleteVariables = options.AutocompleteVariables;
        }

        private VsCompletion InitializeCompletion(string displayName, string insertText, string fullName, string description, string type, StandardGlyphGroup group) =>
            new VsCompletion(displayName, insertText, $"({type}) {fullName}{description}", _glyphService.GetGlyph(group, StandardGlyphItem.GlyphItemPublic), type);

        private static string GetFullName(IBaseToken token) =>
            token.TokenType != TokenType.Function ? token.TokenName : ((FunctionToken)token).GetFullName();

        private static string GetTokenDescription(IBaseToken token) => 
            (token is IDescriptionToken descriptionToken && !string.IsNullOrWhiteSpace(descriptionToken.Description))
                ? Environment.NewLine + Environment.NewLine + ((IDescriptionToken)token).Description
                : string.Empty;
    }
}
