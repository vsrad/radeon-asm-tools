using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighliter
{
    internal class ErrorSpanTag : TextMarkerTag
    {
        private static string GetColorFormat()
        {
            return "syntax error";
        }

        public ErrorSpanTag() : base(GetColorFormat()) { }
    }

    internal class ErrorHighlighterTagger : ITagger<ErrorSpanTag>
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

        public IEnumerable<ITagSpan<ErrorSpanTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var currentWord = new SnapshotSpan(buffer.CurrentSnapshot, 0, 4);
            yield return new TagSpan<ErrorSpanTag>(currentWord, new ErrorSpanTag());
        }
    }
}
