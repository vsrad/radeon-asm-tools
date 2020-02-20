#if false
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Package.Utils;
using OrderAttribute = Microsoft.VisualStudio.Utilities.OrderAttribute;

namespace VSRAD.Package.ProjectSystem.EditorExtensions
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Order]
    [ContentType("any")]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class EvaluateSelectedQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        [Import]
        internal QuickInfoEvaluateSelectedState EvaluateSelectedState { get; set; }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            // Ensure only one instance per text buffer is created
            return textBuffer.Properties.GetOrCreateSingletonProperty(() =>
                new EvaluateSelectedQuickInfoSource(textBuffer, EvaluateSelectedState));
        }
    }

    internal sealed class EvaluateSelectedQuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly ITextBuffer _textBuffer;
        private readonly QuickInfoEvaluateSelectedState _state;

        public EvaluateSelectedQuickInfoSource(ITextBuffer textBuffer, QuickInfoEvaluateSelectedState state)
        {
            _textBuffer = textBuffer;
            _state = state;
        }

        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            if (!_state.HasAnyEvaluatedWatches) return Task.FromResult<QuickInfoItem>(null);

            var maybeExtentSpan = session.GetTriggerPoint(_textBuffer.CurrentSnapshot)?.GetWordExtentSpan();
            if (!maybeExtentSpan.HasValue) return Task.FromResult<QuickInfoItem>(null);

            var wordExtent = maybeExtentSpan.Value;
            var watchName = wordExtent.GetText();

            if (_state.TryGetEvaluated(watchName, out var evaluated))
            {
                var applicableToSpan = _textBuffer.CurrentSnapshot.CreateTrackingSpan(
                    wordExtent.Span.Start, watchName.Length, SpanTrackingMode.EdgeInclusive
                );
                var displayElement = new ContainerElement(
                    ContainerElementStyle.Stacked,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, watchName)),
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Literal, evaluated.AppendByDelimiter('|')))
                );

                return Task.FromResult(new QuickInfoItem(applicableToSpan, displayElement));
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        public void Dispose() { /* No cleanup required */ }
    }

    internal static class EvaluateSelectedWordExtent
    {
        internal static bool IsValidWordChar(this char c) =>
            char.IsLetterOrDigit(c) || c == '_' || c == '$';

        // Loosely based on NaturalLanguageNavigator
        // ITextStructureNavigator.GetExtentOfWord freezes Visual Studio when run on a C/C++ file,
        // so we need to reimplement it ourselves.
        internal static SnapshotSpan? GetWordExtentSpan(this SnapshotPoint snapshot)
        {
            var line = snapshot.GetContainingLine();
            var lineContents = line.GetText();
            var start = FindStartOfWord(snapshot, line, lineContents);
            var end = FindEndOfWord(snapshot, line, lineContents);
            if (start > end) return null;
            return new SnapshotSpan(start, end);
        }

        private static SnapshotPoint FindStartOfWord(SnapshotPoint position, ITextSnapshotLine line, string lineContents)
        {
            if (position == 0)
            {
                return new SnapshotPoint(position.Snapshot, 0);
            }
            int start = line.Start;
            for (int lineIter = Math.Min(position - start /* may be equal to line.End */, lineContents.Length - 1); lineIter >= 0; lineIter--)
            {
                if (!lineContents[lineIter].IsValidWordChar())
                {
                    return new SnapshotPoint(position.Snapshot, start + lineIter + 1);
                }
            }
            return line.Start;
        }

        private static SnapshotPoint FindEndOfWord(SnapshotPoint position, ITextSnapshotLine line, string lineContents)
        {
            if (position >= position.Snapshot.Length - 1)
            {
                return new SnapshotPoint(position.Snapshot, position.Snapshot.Length);
            }
            int start = line.Start;
            int end = line.End;
            for (int lineIter = position - start; lineIter < lineContents.Length; lineIter++)
            {
                if (!lineContents[lineIter].IsValidWordChar())
                {
                    return new SnapshotPoint(position.Snapshot, lineIter + start);
                }
            }
            return line.End;
        }
    }
}
#endif