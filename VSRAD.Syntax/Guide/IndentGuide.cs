using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Blocks;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Formatting;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Windows.Media;
using Task = System.Threading.Tasks.Task;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Guide
{
    internal sealed class IndentGuide
    {
        private readonly IWpfTextView _textView;
        private readonly IAdornmentLayer _layer;
        private readonly Canvas _canvas;
        private readonly DocumentAnalysis _documentAnalysis;
        private IList<Line> _currentAdornments;
        private bool _isEnables;
        private int _tabSize;

        public IndentGuide(IWpfTextView textView, DocumentAnalysis documentAnalysis, OptionsProvider optionsProvider)
        {
            _textView = textView ?? throw new NullReferenceException();
            _tabSize = textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);
            _documentAnalysis = documentAnalysis;

            _currentAdornments = new List<Line>();
            _canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _layer = _textView.GetAdornmentLayer(Constants.IndentGuideAdornmentLayerName) ?? throw new NullReferenceException();
            _isEnables = optionsProvider.IsEnabledIndentGuides;

            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, null, _canvas, CanvasRemoved);
            _textView.LayoutChanged += (sender, args) => UpdateIndentGuides();
            _documentAnalysis.ParserUpdated += ParserUpdated;
            optionsProvider.OptionsUpdated += IndentGuideOptionsUpdated;
            textView.Options.OptionChanged += TabSizeOptionsChanged;
        }

        private void TabSizeOptionsChanged(object sender, EditorOptionChangedEventArgs e)
        {
            _tabSize = _textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);
            UpdateIndentGuides();
        }

        private void ParserUpdated(ITextSnapshot version, IReadOnlyList<IBlock> blocks) =>
            UpdateIndentGuides();

        private void IndentGuideOptionsUpdated(OptionsProvider sender)
        {
            if (sender.IsEnabledIndentGuides != _isEnables)
            {
                _isEnables = sender.IsEnabledIndentGuides;
                if (_isEnables)
                    UpdateIndentGuides();
                else
                    CleanupIndentGuidesAsync().ConfigureAwait(false);
            }
        }

        private void CanvasRemoved(object tag, UIElement element) =>
            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, null, _canvas, CanvasRemoved);

        private void UpdateIndentGuides()
        {
            if (_isEnables)
                ThreadHelper.JoinableTaskFactory.RunAsync(SetupIndentGuidesAsync);
        }

        private async Task CleanupIndentGuidesAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            foreach (var oldIndentGuide in _currentAdornments)
            {
                _canvas.Children.Remove(oldIndentGuide);
            }
            _currentAdornments = new List<Line>();
        }

        private async Task SetupIndentGuidesAsync()
        {
            try
            {
                if (_textView.InLayout)
                    return;

                var firstVisibleLine = _textView.TextViewLines.First(line => line.IsFirstTextViewLineForSnapshotLine);
                var lastVisibleLine = _textView.TextViewLines.Last(line => line.IsLastTextViewLineForSnapshotLine);

                var newSpanElements = _documentAnalysis
                    .LastParserResult
                    .Where(b => b.Type != BlockType.Root && b.Type != BlockType.Comment && IsInVisualBuffer(b, firstVisibleLine, lastVisibleLine));

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var updatedIndentGuides = GetUpdatedIndentGuides(newSpanElements, firstVisibleLine.TextLeft, firstVisibleLine.VirtualSpaceWidth);

                ClearAndUpdateCurrentGuides(updatedIndentGuides);
            }
            catch (ObjectDisposedException) 
            {
                // If the buffer was changed during the calculation of the first and last line
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        private static bool IsInVisualBuffer(IBlock block, ITextViewLine firstVisibleLine, ITextViewLine lastVisibleLine)
        {
            var blockStart = block.TokenStart.Start.GetPoint(firstVisibleLine.Snapshot);
            var blockEnd = block.TokenEnd.GetEnd(firstVisibleLine.Snapshot);

            bool isOnStart = blockStart <= lastVisibleLine.End;
            bool isOnEnd = blockEnd >= firstVisibleLine.End;

            bool isInBlockAll = (blockStart <= firstVisibleLine.End) && (blockEnd >= lastVisibleLine.Start);

            return isOnStart && isOnEnd || isInBlockAll;
        }

        private IEnumerable<Line> GetUpdatedIndentGuides(IEnumerable<IBlock> blocks, double horizontalOffset, double spaceWidth)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (var block in blocks)
            {
                var pointStart = block.TokenStart.Start.GetPoint(_textView.TextSnapshot);
                var pointEnd = new SnapshotPoint(_textView.TextSnapshot, block.TokenEnd.GetEnd(_textView.TextSnapshot));

                var viewLineStart = _textView.GetTextViewLineContainingBufferPosition(pointStart);
                var viewLineEnd = _textView.GetTextViewLineContainingBufferPosition(pointEnd);

                if (viewLineStart.Equals(viewLineEnd)) continue;

                var lineStart = pointStart.GetContainingLine();
                var spaceText = new SnapshotSpan(lineStart.Start, pointStart).GetText();
                var tabs = spaceText.Count(ch => ch == '\t');

                var indentStart = (spaceText.Length - tabs) + tabs * _tabSize;
                var leftOffset = indentStart * spaceWidth + horizontalOffset;

                yield return new Line()
                {
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection() { 2 },
                    X1 = leftOffset,
                    X2 = leftOffset,
                    Y1 = (viewLineStart.Top != 0) ? viewLineStart.Bottom : _textView.ViewportTop,
                    Y2 = (viewLineEnd.Top != 0) ? viewLineEnd.Top : _textView.ViewportBottom,
                };
            }
        }

        private void ClearAndUpdateCurrentGuides(IEnumerable<Line> newIndentGuides)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _canvas.Visibility = Visibility.Visible;

            foreach (var oldIndentGuide in _currentAdornments)
            {
                _canvas.Children.Remove(oldIndentGuide);
            }

            _currentAdornments = newIndentGuides.ToList();

            foreach (var newIndentGuide in _currentAdornments)
            {
                _canvas.Children.Add(newIndentGuide);
            }
        }
    }
}