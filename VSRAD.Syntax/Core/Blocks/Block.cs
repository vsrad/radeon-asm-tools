using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Blocks
{
    public enum BlockType
    {
        Root,
        Function,
        Comment,
        Condition,
        Repeat,
        Loop,
    }

    public interface IBlock
    {
        IBlock Parent { get; }
        ITextSnapshot Snapshot { get; }
        TrackingBlock Scope { get; }
        BlockType Type { get; }
        List<IBlock> Children { get; }
        List<AnalysisToken> Tokens { get; }
        int ActualStart { get; }
        int ActualEnd { get; }

        void AddChildren(IBlock block);
        void AddToken(AnalysisToken token);
        bool InScope(int point);
        bool InScope(Span span);
        bool InRange(int point);
        bool InRange(Span span);
        void SetEnd(int endPosition, TrackingToken tokenEnd);
        void SetStart(int startPosition);
    }

    internal class Block : IBlock
    {
        public IBlock Parent { get; }
        public ITextSnapshot Snapshot { get; }
        public BlockType Type { get; }
        public List<IBlock> Children { get; }
        public List<AnalysisToken> Tokens { get; }
        public TrackingBlock Scope { get; private set; }

        public int ActualStart { get; }
        public int ActualEnd { get; private set; }

        private int startPosition;

        public Block(IBlock parrent, BlockType type, TrackingToken tokenStart)
        {
            Parent = parrent;
            Type = type;
            Snapshot = parrent.Snapshot;
            ActualStart = tokenStart.GetStart(Snapshot);

            Children = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public Block(IBlock parrent, BlockType type, TrackingToken tokenStart, TrackingToken tokenEnd)
        {
            Parent = parrent;
            Type = type;
            Snapshot = parrent.Snapshot;
            ActualStart = tokenStart.GetStart(Snapshot);
            ActualEnd = tokenEnd.GetEnd(Snapshot);
            Scope = new TrackingBlock(Snapshot, ActualStart, ActualEnd);

            Children = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public Block(ITextSnapshot snapshot)
        {
            Parent = null;
            Type = BlockType.Root;
            Snapshot = snapshot;
            ActualStart = 0;
            ActualEnd = Snapshot.Length - 1;

            Children = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public void SetStart(int scopeStart) =>
            startPosition = scopeStart;

        public void SetEnd(int endPosition, TrackingToken tokenEnd)
        {
            SetScopeEnd(endPosition);
            ActualEnd = tokenEnd.GetEnd(Snapshot);
        }

        public virtual void AddChildren(IBlock block) =>
            Children.Add(block);

        public virtual void AddToken(AnalysisToken token) =>
            Tokens.Add(token);

        public virtual bool InScope(int point) =>
            Scope.GetSpan(Snapshot).Contains(point);

        public virtual bool InScope(Span span) =>
            Scope.GetSpan(Snapshot).Contains(span);

        public virtual bool InRange(int point) =>
            ActualStart <= point && ActualEnd >= point;

        public virtual bool InRange(Span span) =>
            ActualStart <= span.Start && ActualEnd >= span.End;

        private void SetScopeEnd(int endPosition)
        {
            if (startPosition < endPosition)
                Scope = new TrackingBlock(Snapshot, startPosition, endPosition);
        }
    }
}
