using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
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

        public CompletionSource(
            IGlyphService glyphService,
            ITextStructureNavigator textStructureNavigator,
            InstructionListManager instructionListManager)
        {
            _textStructureNavigator = textStructureNavigator;
            _glyphService = glyphService;

            instructionListManager.InstructionUpdated += InstructionUpdatedEvent;
            UpdateInstructionCompletions(instructionListManager.InstructionList);
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            try
            {
                completionSets.Add(new CompletionSet(
                    "RadInstructions",    //the non-localized title of the tab
                    "RadInstructions",    //the display title of the tab
                    FindTokenSpanAtPosition(session),
                    _instructionCompletions,
                    null));

                completionSets.Add(new CompletionSet(
                    "RadTokens",    //the non-localized title of the tab
                    "RadTokens",    //the display title of the tab
                    FindTokenSpanAtPosition(session),
                    GetScopedCompletions(session),
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

        private IEnumerable<VsCompletion> GetScopedCompletions(ICompletionSession session)
        {
            var scopedCompletions = new List<VsCompletion>();

            var parserManager = session.TextView.GetParserManager();
            var parser = parserManager?.ActualParser;
            if (parser == null)
                return scopedCompletions;

            var scopedTokens = parser
                .GetScopedTokens(session.TextView.Caret.Position.BufferPosition)
                .OrderBy(t => t.TokenName);
            foreach (var token in scopedTokens)
            {
                switch (token.TokenType)
                {
                    case TokenType.Argument:
                        scopedCompletions.Add(InitializeCompletion(token.TokenName, "argument", StandardGlyphGroup.GlyphGroupVariable));
                        break;
                    case TokenType.Function:
                        scopedCompletions.Add(InitializeCompletion(token.TokenName, "function", StandardGlyphGroup.GlyphGroupMethod));
                        break;
                    case TokenType.Label:
                        scopedCompletions.Add(InitializeCompletion(token.TokenName, "label", StandardGlyphGroup.GlyphGroupNamespace));
                        break;
                    case TokenType.GlobalVariable:
                        scopedCompletions.Add(InitializeCompletion(token.TokenName, "global variable", StandardGlyphGroup.GlyphGroupVariable));
                        break;
                    case TokenType.LocalVariable:
                        scopedCompletions.Add(InitializeCompletion(token.TokenName, "local variable", StandardGlyphGroup.GlyphGroupVariable));
                        break;
                    default:
                        scopedCompletions.Add(InitializeCompletion(token.TokenName, "unknown", StandardGlyphGroup.GlyphGroupUnknown));
                        break;
                }
            }
            return scopedCompletions;
        }

        private void UpdateInstructionCompletions(IEnumerable<string> instructions)
        {
            _instructionCompletions = instructions
                .OrderBy(i => i)
                .Select(i => InitializeCompletion(i, "instruction", StandardGlyphGroup.GlyphGroupField));
        }

        private void InstructionUpdatedEvent(IReadOnlyList<string> instructions) =>
            UpdateInstructionCompletions(instructions);

        private VsCompletion InitializeCompletion(string text, string type, StandardGlyphGroup group) =>
            new VsCompletion(text, text, $"({type}) {text}", _glyphService.GetGlyph(group, StandardGlyphItem.GlyphItemPublic), type);
    }
}
