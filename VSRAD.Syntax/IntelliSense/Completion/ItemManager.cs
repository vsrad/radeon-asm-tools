using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.PatternMatching;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal class ItemManager : IAsyncCompletionItemManager
    {
        private readonly IPatternMatcherFactory _patternMatcherFactory;
        private readonly IDocumentFactory _documentFactory;

        public ItemManager(IPatternMatcherFactory patternMatcherFactory, IDocumentFactory documentFactory)
        {
            _patternMatcherFactory = patternMatcherFactory;
            _documentFactory = documentFactory;
        }

        public Task<ImmutableArray<CompletionItem>> SortCompletionListAsync(IAsyncCompletionSession session, AsyncCompletionSessionInitialDataSnapshot data, CancellationToken token) =>
            Task.FromResult(data.InitialList.OrderBy(i => i.SortText).ToImmutableArray());

        public Task<FilteredCompletionModel> UpdateCompletionListAsync(IAsyncCompletionSession session, AsyncCompletionSessionDataSnapshot data, CancellationToken token) =>
            Task.FromResult(UpdateCompletionList(session, data, token));

        private FilteredCompletionModel UpdateCompletionList(IAsyncCompletionSession session, AsyncCompletionSessionDataSnapshot data, CancellationToken token)
        {
            var triggerReason = data.Trigger.Reason;
            var initialTriggerReason = data.InitialTrigger.Reason;

            if (initialTriggerReason == CompletionTriggerReason.Insertion
                && triggerReason == CompletionTriggerReason.Backspace
                || initialTriggerReason == CompletionTriggerReason.Deletion)
            {
                return null;
            }

            var filterText = session.ApplicableToSpan.GetText(data.Snapshot);
            var startPoint = session.ApplicableToSpan.GetStartPoint(data.Snapshot);

            if (string.IsNullOrWhiteSpace(filterText) || IsDemissCharacter(filterText[filterText.Length - 1]))
                return null;

            if (initialTriggerReason == CompletionTriggerReason.Insertion)
            {
                var document = _documentFactory.GetOrCreateDocument(startPoint.Snapshot.TextBuffer);
                if (document == null) return null;

                var triggerToken = document.DocumentTokenizer.CurrentResult.GetToken(startPoint);
                var triggerTokenType = document.DocumentTokenizer.GetTokenType(triggerToken.Type);

                if (triggerTokenType == RadAsmTokenType.Keyword ||
                    triggerTokenType == RadAsmTokenType.Preprocessor ||
                    triggerTokenType == RadAsmTokenType.Number ||
                    triggerTokenType == RadAsmTokenType.Comment)
                {
                    return null;
                }
            }

            // Pattern matcher not only filters, but also provides a way to order the results by their match quality.
            // The relevant CompletionItem is match.Item1, its PatternMatch is match.Item2
            var patternMatcher = _patternMatcherFactory.CreatePatternMatcher(
                filterText,
                new PatternMatcherCreationOptions(System.Globalization.CultureInfo.CurrentCulture, PatternMatcherCreationFlags.IncludeMatchedSpans | PatternMatcherCreationFlags.AllowFuzzyMatching));

            token.ThrowIfCancellationRequested();
            var matches = data.InitialSortedList
                // Perform pattern matching
                .Select(completionItem => (completionItem, patternMatcher.TryMatch(completionItem.FilterText)))
                // Pick only items that were matched, unless length of filter text is 1
                .Where(n => (filterText.Length == 1 || n.Item2.HasValue));

            // See which filters might be enabled based on the typed code
            var textFilteredFilters = matches.SelectMany(n => n.completionItem.Filters).Distinct();

            // When no items are available for a given filter, it becomes unavailable
            var updatedFilters = ImmutableArray.CreateRange(data.SelectedFilters.Select(n => n.WithAvailability(textFilteredFilters.Contains(n.Filter))));

            // Filter by user-selected filters. The value on availableFiltersWithSelectionState conveys whether the filter is selected.
            var filterFilteredList = matches;
            if (data.SelectedFilters.Any(n => n.IsSelected))
            {
                filterFilteredList = matches.Where(n => ShouldBeInCompletionList(n.completionItem, data.SelectedFilters));
            }

            var bestMatch = filterFilteredList.OrderByDescending(n => n.Item2.HasValue).ThenBy(n => n.Item2).FirstOrDefault();
            var listWithHighlights = filterFilteredList.Select(n =>
            {
                token.ThrowIfCancellationRequested();

                var safeMatchedSpans = ImmutableArray<Span>.Empty;
                if (n.completionItem.DisplayText == n.completionItem.FilterText)
                {
                    if (n.Item2.HasValue)
                    {
                        safeMatchedSpans = n.Item2.Value.MatchedSpans;
                    }
                }
                else
                {
                    // Matches were made against FilterText. We are displaying DisplayText. To avoid issues, re-apply matches for these items
                    var newMatchedSpans = patternMatcher.TryMatch(n.completionItem.DisplayText);
                    if (newMatchedSpans.HasValue)
                    {
                        safeMatchedSpans = newMatchedSpans.Value.MatchedSpans;
                    }
                }

                if (safeMatchedSpans.IsDefaultOrEmpty)
                {
                    return new CompletionItemWithHighlight(n.completionItem);
                }
                else
                {
                    return new CompletionItemWithHighlight(n.completionItem, safeMatchedSpans);
                }
            }).ToImmutableArray();

            if (listWithHighlights.Length == 0)
                return null;

            int selectedItemIndex = 0;
            if (data.DisplaySuggestionItem)
            {
                selectedItemIndex = -1;
            }
            else
            {
                for (int i = 0; i < listWithHighlights.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (listWithHighlights[i].CompletionItem == bestMatch.completionItem)
                    {
                        selectedItemIndex = i;
                        break;
                    }
                }
            }

            return new FilteredCompletionModel(listWithHighlights, selectedItemIndex, updatedFilters);
        }

        private static bool ShouldBeInCompletionList(CompletionItem item, ImmutableArray<CompletionFilterWithState> filtersWithState)
        {
            foreach (var filterWithState in filtersWithState.Where(n => n.IsSelected))
            {
                if (item.Filters.Any(n => n == filterWithState.Filter))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsDemissCharacter(char ch)
        {
            if (ch == ' ' || 
                ch == '(' || 
                ch == ')' || 
                ch == '[' || 
                ch == ']' ||
                ch == ',' ||
                ch == ':')
            {
                return true;
            }

            return false;
        }
    }
}
