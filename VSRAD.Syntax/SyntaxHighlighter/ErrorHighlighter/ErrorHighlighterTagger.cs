using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter
{
    internal class ErrorHighlighterTagger : ITagger<IErrorTag>
    {
        private readonly ITextView view;
        private readonly ITextBuffer buffer;
        private readonly object updateLock;
        private NormalizedSnapshotSpanCollection ErorSpans { get; set; }
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        internal ErrorHighlighterTagger(ITextView textView, ITextBuffer sourceBuffer)
        {
            view = textView;
            buffer = sourceBuffer;
            updateLock = new object();

            view.Caret.PositionChanged += CaretPositionChanged;
            ErorSpans = new NormalizedSnapshotSpanCollection();
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) => UpdateErorMarker();

        private void UpdateErorMarker()
        {
            ThreadPool.QueueUserWorkItem(UpdateSpanAdornments);
        }

        private void UpdateSpanAdornments(object threadContext)
        {
            SynchronousUpdate();
        }

        private void SynchronousUpdate()
        {
            lock (updateLock)
            {
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length)));
            }
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var currentWord = new SnapshotSpan(buffer.CurrentSnapshot, 0, 4);
            yield return new TagSpan<IErrorTag>(currentWord, new ErrorTag(PredefinedErrorTypeNames.SyntaxError));
        }
    }
}
