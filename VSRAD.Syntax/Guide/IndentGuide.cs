﻿using VSRAD.Syntax.Parser;
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
using Task = System.Threading.Tasks.Task;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.Guide
{
    internal sealed class IndentGuide
    {
        private readonly IWpfTextView _wpfTextView;
        private readonly IParserManager _parserManager;
        private readonly IAdornmentLayer _layer;
        private readonly Canvas _canvas;
        private IBaseParser _currentParser;
        private IList<Line> _currentAdornments;
        private bool _isEnables;

        public IndentGuide(IWpfTextView textView, IParserManager parserManager, OptionsProvider optionsProvider)
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
            _isEnables = optionsProvider.IsEnabledIndentGuides;

            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, null, _canvas, CanvasRemoved);
            _wpfTextView.LayoutChanged += async (sender, args) => await UpdateIndentGuidesAsync();
            _parserManager.ParserUpdatedEvent += async (sender, args) => await UpdateIndentGuidesAsync();
            optionsProvider.OptionsUpdated += IndentGuideOptionsUpdated;
        }

        private void IndentGuideOptionsUpdated(OptionsProvider sender)
        {
            _isEnables = sender.IsEnabledIndentGuides;
            ThreadHelper.JoinableTaskFactory.RunAsync(UpdateIndentGuidesAsync);
        }

        private void CanvasRemoved(object tag, UIElement element)
        {
            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, null, _canvas, CanvasRemoved);
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
                if (!_isEnables)
                {
                    await CleanupIndentGuidesAsync();
                    return;
                }

                var firstVisibleLine = _wpfTextView.TextViewLines.First(line => line.IsFirstTextViewLineForSnapshotLine);
                var lastVisibleLine = _wpfTextView.TextViewLines.Last(line => line.IsLastTextViewLineForSnapshotLine);

                var newSpanElements = _currentParser.ListBlock.Where(block => block.BlockType != BlockType.Root && block.BlockType != BlockType.Comment && IsInVisualBuffer(block, firstVisibleLine, lastVisibleLine));

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var updatedIndentGuides = GetUpdatedIndentGuides(newSpanElements, firstVisibleLine.TextLeft, firstVisibleLine.VirtualSpaceWidth);

                ClearAndUpdateCurrentGuides(updatedIndentGuides);
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        private static bool IsInVisualBuffer(IBaseBlock block, ITextViewLine firstVisibleLine, ITextViewLine lastVisibleLine)
        {
            bool isOnStart = block.BlockSpan.Start <= lastVisibleLine.End;
            bool isOnEnd = block.BlockSpan.End >= firstVisibleLine.End;

            bool isInBlockAll = (block.BlockSpan.Start <= firstVisibleLine.End) && (block.BlockSpan.End >= lastVisibleLine.Start);

            return isOnStart && isOnEnd || isInBlockAll;
        }

        private IEnumerable<Line> GetUpdatedIndentGuides(IEnumerable<IBaseBlock> blocks, double horizontalOffset, double spaceWidth)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (var block in blocks)
            {
                var span = block.BlockActualSpan;

                if (span.Snapshot != _wpfTextView.TextSnapshot)
                    yield break;

                var viewLineStart = _wpfTextView.GetTextViewLineContainingBufferPosition(span.Start);
                var viewLineEnd = _wpfTextView.GetTextViewLineContainingBufferPosition(span.End);

                if (viewLineStart.Equals(viewLineEnd)) continue;

                var indentStart = block.SpaceStart;
                var leftOffset = indentStart * spaceWidth + horizontalOffset;

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

        private Task UpdateIndentGuidesAsync()
        {
            _currentParser = _parserManager.ActualParser;

            return _currentParser == null ? Task.CompletedTask : SetupIndentGuidesAsync();
        }
    }
}