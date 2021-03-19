using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Deborgar;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.EditorExtensions
{
    public sealed class BreakLineGlyphTag : IGlyphTag
    {
        public string ToolTip { get; }

        public BreakLineGlyphTag(string toolTip)
        {
            ToolTip = toolTip;
        }
    }

    [Export(typeof(IViewTaggerProvider))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    [ContentType("any")]
    [TagType(typeof(BreakLineGlyphTag))]
    public class BreakLineGlyphTaggerProvider : IViewTaggerProvider
    {
        // Tagger provider can be instantiated before UnconfiguredProject,
        // so we can't use ImportingConstructor to access DebuggerIntegration's
        // ExecutionCompleted event directly.
        internal event EventHandler<ExecutionCompletedEventArgs> DebugExecutionCompleted;

        public virtual void OnExecutionCompleted(ExecutionCompletedEventArgs execCompleted)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                VsEditor.NavigateToFileAndLine(ServiceProvider.GlobalProvider, execCompleted.File, execCompleted.Lines[0]);

                // Clear VS debugger break markers because they may be stale (clicking Debug in our toolbar does not update them, for instance)
                var vsDebuggerBreakLineMarkers = VsEditor.GetLineMarkersOfTypeInActiveView(ServiceProvider.GlobalProvider, 63);
                foreach (var m in vsDebuggerBreakLineMarkers)
                    m.Invalidate();

                // Draw our own markers
                DebugExecutionCompleted?.Invoke(this, execCompleted);
            }
            catch (Exception e)
            {
                Errors.ShowCritical($"An error occurred while showing the current breakpoint location: {e.Message}\r\n\r\n{e.StackTrace}");
            }
        }

        public void RemoveBreakLineMarkers()
        {
            try
            {
                DebugExecutionCompleted?.Invoke(this, null);
            }
            catch (Exception e)
            {
                Errors.ShowCritical($"An error occurred while clearing break line markers: {e.Message}\r\n\r\n{e.StackTrace}");
            }
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (buffer != null && buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
                return buffer.Properties.GetOrCreateSingletonProperty(() => new BreakLineGlyphTagger(buffer, document, this) as ITagger<T>);
            return null;
        }
    }

    public sealed class BreakLineGlyphTagger : ITagger<BreakLineGlyphTag>
    {
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private readonly ITextBuffer _buffer;
        private readonly ITextDocument _document;

        private readonly List<TagSpan<BreakLineGlyphTag>> _tagSpans = new List<TagSpan<BreakLineGlyphTag>>();

        public BreakLineGlyphTagger(ITextBuffer buffer, ITextDocument document, BreakLineGlyphTaggerProvider provider)
        {
            _buffer = buffer;
            _document = document;
            provider.DebugExecutionCompleted += DebugExecutionCompleted;
        }

        private void DebugExecutionCompleted(object sender, ExecutionCompletedEventArgs e)
        {
            _tagSpans.Clear();

            if (e != null && e.File == _document.FilePath)
            {
                var toolTip = "Last RAD debugger break " + (e.Lines.Length == 1
                            ? $"line: {e.Lines[0]}"
                            : "lines: " + string.Join(", ", e.Lines));

                foreach (var line in e.Lines)
                {
                    if (line >= _buffer.CurrentSnapshot.LineCount)
                        continue;

                    var snapshotLine = _buffer.CurrentSnapshot.GetLineFromLineNumber((int)line);
                    var tagSpan = new SnapshotSpan(snapshotLine.Start, snapshotLine.End);
                    _tagSpans.Add(new TagSpan<BreakLineGlyphTag>(tagSpan, new BreakLineGlyphTag(toolTip)));
                }
            }

            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
        }

        public IEnumerable<ITagSpan<BreakLineGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans) => _tagSpans;
    }
}
