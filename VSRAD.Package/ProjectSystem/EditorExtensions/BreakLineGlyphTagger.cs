using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Deborgar;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.EditorExtensions
{
    public sealed class BreakLineGlyphTag : IGlyphTag
    {
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
        internal event EventHandler<EventArgs> BreakpointsHitChanged;

        // Need to store the breakpoints from the last execution because new taggers may be created at any time and we want them to display the latest breakpoint markers.
        internal List<BreakLocation> LastExecutionBreakpointsHit { get; } = new List<BreakLocation>();

        public virtual void OnExecutionCompleted(IProjectSourceManager sourceManager, ExecutionCompletedEventArgs execCompleted)
        {
            LastExecutionBreakpointsHit.Clear();
            if (execCompleted.IsSuccessful)
                LastExecutionBreakpointsHit.AddRange(execCompleted.BreakLocations);

#pragma warning disable VSTHRD001 // Need to schedule execution after VS debugger adds its line markers 
            ThreadHelper.Generic.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
#pragma warning restore VSTHRD001
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    // Clear VS debugger break markers because:
                    // 1) They shouldn't be shown when execution fails, but our debugger needs to report the current caret position as the break line, otherwise
                    //    VS switches focus to a "Source Not Available" tab, however the break line marker shouldn't be displayed in the editor as no break occurred.
                    // 2) The markers may be stale: clicking Debug in our toolbar does not update them, for instance.
                    var sourcePaths = execCompleted.BreakLocations.Select(i => i.CallStack[0].SourcePath).Where(p => !string.IsNullOrEmpty(p)).Distinct();
                    foreach (var path in sourcePaths)
                    {
                        var textBuffer = sourceManager.GetDocumentTextBufferByPath(path);
                        if (textBuffer != null)
                        {
                            var vsDebuggerBreakLineMarkers = VsEditor.GetTextLineMarkersOfType(textBuffer, 63).Concat(VsEditor.GetTextLineMarkersOfType(textBuffer, 64));
                            foreach (var m in vsDebuggerBreakLineMarkers)
                                m.Invalidate();
                        }
                    }
                    // Draw our own markers instead
                    BreakpointsHitChanged?.Invoke(this, new EventArgs());
                }
                catch (Exception e)
                {
                    Errors.ShowCritical($"An error occurred while showing the current breakpoint location: {e.Message}\r\n\r\n{e.StackTrace}");
                }
            });
        }

        public void RemoveBreakLineMarkers()
        {
            try
            {
                LastExecutionBreakpointsHit.Clear();
                BreakpointsHitChanged?.Invoke(this, new EventArgs());
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
        private readonly BreakLineGlyphTaggerProvider _provider;

        private readonly List<TagSpan<BreakLineGlyphTag>> _tagSpans = new List<TagSpan<BreakLineGlyphTag>>();

        public BreakLineGlyphTagger(ITextBuffer buffer, ITextDocument document, BreakLineGlyphTaggerProvider provider)
        {
            _buffer = buffer;
            _document = document;
            _provider = provider;
            _provider.BreakpointsHitChanged += BreakpointsHitChanged;
            BreakpointsHitChanged(_provider, new EventArgs());
        }

        private void BreakpointsHitChanged(object sender, EventArgs e)
        {
            _tagSpans.Clear();

            foreach (var breakpoint in _provider.LastExecutionBreakpointsHit)
            {
                var topFrame = breakpoint.CallStack[0];
                if (string.Equals(topFrame.SourcePath, _document.FilePath, StringComparison.OrdinalIgnoreCase) && topFrame.SourceLine < _buffer.CurrentSnapshot.LineCount)
                {
                    var snapshotLine = _buffer.CurrentSnapshot.GetLineFromLineNumber((int)topFrame.SourceLine);
                    var tagSpan = new SnapshotSpan(snapshotLine.Start, snapshotLine.End);
                    _tagSpans.Add(new TagSpan<BreakLineGlyphTag>(tagSpan, new BreakLineGlyphTag()));
                }
            }

            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
        }

        public IEnumerable<ITagSpan<BreakLineGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans) => _tagSpans;
    }
}
