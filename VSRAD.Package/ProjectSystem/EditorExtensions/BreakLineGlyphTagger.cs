using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Deborgar;

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
    public sealed class BreakLineGlyphTaggerProvider : IViewTaggerProvider
    {
        /// Tagger provider can be instantiated either before or after UnconfiguredProject,
        /// so we can't use ImportingConstructor _and_ we can't rely on the tagger being available
        /// when IProject is loaded. This hack shouldn't cause issues as long as there's never more
        /// than one active instance of IProject, which is guaranteed by SolutionManager.
        private static DebuggerIntegration _debuggerIntegration;

        public static void Initialize(IProject project)
        {
            _debuggerIntegration = project.UnconfiguredProject.Services.ExportProvider.GetExportedValue<DebuggerIntegration>();

            void OnProjectUnloaded()
            {
                _debuggerIntegration = null;
                project.Unloaded -= OnProjectUnloaded;
            }
            project.Unloaded += OnProjectUnloaded;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (_debuggerIntegration != null
                && textView.TextBuffer == buffer
                && buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
                return buffer.Properties.GetOrCreateSingletonProperty(() => new BreakLineGlyphTagger(buffer, document, _debuggerIntegration) as ITagger<T>);
            return null;
        }
    }

    public sealed class BreakLineGlyphTagger : ITagger<BreakLineGlyphTag>
    {
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private readonly ITextBuffer _buffer;
        private readonly ITextDocument _document;

        private readonly List<TagSpan<BreakLineGlyphTag>> _tagSpans = new List<TagSpan<BreakLineGlyphTag>>();

        public BreakLineGlyphTagger(ITextBuffer buffer, ITextDocument document, DebuggerIntegration debuggerIntegration)
        {
            _buffer = buffer;
            _document = document;
            debuggerIntegration.ExecutionCompleted += DebugExecutionCompleted;
        }

        private void DebugExecutionCompleted(object sender, ExecutionCompletedEventArgs e)
        {
            _tagSpans.Clear();

            if (e.File != _document.FilePath)
                return;

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

            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
        }

        public IEnumerable<ITagSpan<BreakLineGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans) => _tagSpans;
    }
}
