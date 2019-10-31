using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Blocks;
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

namespace VSRAD.Syntax.Guides
{
    internal sealed class IndentGuide
    {
        private readonly IWpfTextView _wpfTextView;
        private readonly IParserManager _parserManager;
        private readonly IAdornmentLayer _layer;
        private readonly Canvas _canvas;
        private IBaseParser _currentParser;
        private IList<Line> _currentAdornments;

        public IndentGuide(IWpfTextView textView, IParserManager parserManager)
        {
            _wpfTextView = textView ?? throw new NullReferenceException();
            _parserManager = parserManager ?? throw new NullReferenceException();
            _currentAdornments = new List<Line>();
            _canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _layer = _wpfTextView.GetAdornmentLayer(Constants.IndentGuideAdornmentLayerName) ?? throw new NullReferenceException();

            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, null, _canvas, CanvasRemoved);
            _wpfTextView.LayoutChanged += UpdateIndentGuides;
        }

        private void CanvasRemoved(object tag, UIElement element)
        {
            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, null, _canvas, CanvasRemoved);
        }

        private void UpdateIndentGuides()
        {
            try
            {
                SetupIndentGuides();
            }
            catch (Exception e)
            {
                ActivityLog.LogError(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
        }

        private void SetupIndentGuides()
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _canvas.Visibility = Visibility.Visible;

                var firstVisibleLine = _wpfTextView.TextViewLines.First(line => line.IsFirstTextViewLineForSnapshotLine);
                var lastVisibleLine = _wpfTextView.TextViewLines.Last(line => line.IsLastTextViewLineForSnapshotLine);

                var newSpanElements = _currentParser.ListBlock.Where(block => block.BlockType != BlockType.Root && block.BlockType != BlockType.Comment && IsInVisualBuffer(block, firstVisibleLine, lastVisibleLine));
                var updatedIndentGuides = GetUpdatedIndentGuides(newSpanElements, firstVisibleLine.TextLeft, firstVisibleLine.VirtualSpaceWidth);

                ClearAndUpdateCurrentGuides(updatedIndentGuides);
            });
        }

        private bool IsInVisualBuffer(IBaseBlock block, ITextViewLine firstVisibleLine, ITextViewLine lastVisibleLine)
        {
            bool isOnStart = block.BlockSpan.Start <= lastVisibleLine.End;
            bool isOnEnd = block.BlockSpan.End >= firstVisibleLine.End;

            bool isInBlockAll = (block.BlockSpan.Start <= firstVisibleLine.End) && (block.BlockSpan.End >= lastVisibleLine.Start);

            return isOnStart && isOnEnd || isInBlockAll;
        }

        private IEnumerable<Line> GetUpdatedIndentGuides(IEnumerable<IBaseBlock> blocks, double horizontalOffset, double spaceWidth)
        {
            foreach (var block in blocks)
            {
                var span = block.BlockActualSpan;
                var viewLineStart = _wpfTextView.GetTextViewLineContainingBufferPosition(span.Start);
                var viewLineEnd = _wpfTextView.GetTextViewLineContainingBufferPosition(span.End);

                if (viewLineStart.Equals(viewLineEnd)) continue;

                var indentStart = block.SpaceStart;
                var leftOffset = indentStart * spaceWidth + horizontalOffset;
                var brush = Brushes.White;

                yield return new Line()
                {
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection() { 2 },
                    X1 = leftOffset,
                    X2 = leftOffset,
                    Y1 = (viewLineStart.Top != 0) ? viewLineStart.Bottom : _wpfTextView.ViewportTop,
                    Y2 = (viewLineEnd.Top != 0) ? viewLineEnd.Top : _wpfTextView.ViewportBottom,
                };
            }
        }

        private void ClearAndUpdateCurrentGuides(IEnumerable<Line> newIndentGuides)
        {
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

        private void UpdateIndentGuides(object actualParser, object _)
        {
            _currentParser = _parserManager.ActualParser;

            UpdateIndentGuides();
        }
    }
}