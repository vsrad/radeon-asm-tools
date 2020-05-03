using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class CompletionSource : ICompletionSource
    {
        private readonly ITextStructureNavigator _textStructureNavigator;
        private readonly IGlyphService _glyphService;
        private IEnumerable<Microsoft.VisualStudio.Language.Intellisense.Completion> _completions;
        private bool _isDisposed;

        public CompletionSource(
            IGlyphService glyphService,
            ITextStructureNavigator textStructureNavigator,
            InstructionListManager instructionListManager)
        {
            _textStructureNavigator = textStructureNavigator;
            _glyphService = glyphService;

            instructionListManager.InstructionUpdated += InstructionUpdatedEvent;
            UpdateCompletions(instructionListManager.InstructionList);
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            try
            {
                completionSets.Add(new CompletionSet(
                    "RadInstructions",    //the non-localized title of the tab
                    "RadInstructions",    //the display title of the tab
                    FindTokenSpanAtPosition(session),
                    _completions,
                    null));
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

        private ITrackingSpan FindTokenSpanAtPosition(ICompletionSession session)
        {
            var currentPoint = session.TextView.Caret.Position.BufferPosition;
            // show set after only two characters
            if (currentPoint > 2)
                currentPoint -= 2;

            var extent = _textStructureNavigator.GetExtentOfWord(currentPoint);
            return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
        }

        private void UpdateCompletions(IEnumerable<string> instructions)
        {
            _completions = instructions
                .OrderBy(i => i)
                .Select(i => new Microsoft.VisualStudio.Language.Intellisense.Completion(i, i, $"(instruction) {i}", _glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPublic), "instruction"));
        }

        private void InstructionUpdatedEvent(IReadOnlyList<string> instructions) =>
            UpdateCompletions(instructions);
    }
}
