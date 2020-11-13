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
        TrackingBlock Area { get; }
        BlockType Type { get; }
        List<IBlock> Children { get; }
        List<AnalysisToken> Tokens { get; }

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
        public TrackingBlock Area { get; private set; }

        protected int actualStart;
        protected int actualEnd;
        private int startPosition;

        public Block(IBlock parrent, BlockType type, TrackingToken tokenStart)
        {
            Parent = parrent;
            Type = type;
            Snapshot = parrent.Snapshot;

            actualStart = tokenStart.GetStart(Snapshot);
            actualEnd = Snapshot.Length - 1;
            Scope = new TrackingBlock(Snapshot, actualStart, actualEnd);
            Area = new TrackingBlock(Snapshot, actualStart, actualEnd);

            Children = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public Block(IBlock parrent, BlockType type, TrackingToken tokenStart, TrackingToken tokenEnd)
        {
            Parent = parrent;
            Type = type;
            Snapshot = parrent.Snapshot;

            actualStart = tokenStart.GetStart(Snapshot);
            actualEnd = tokenEnd.GetEnd(Snapshot);
            Scope = new TrackingBlock(Snapshot, actualStart, actualEnd);
            Area = new TrackingBlock(Snapshot, actualStart, actualEnd);

            Children = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public Block(ITextSnapshot snapshot)
        {
            Parent = null;
            Type = BlockType.Root;
            Snapshot = snapshot;
            actualStart = 0;
            actualEnd = Snapshot.Length - 1;

            Children = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public void SetStart(int scopeStart) =>
            startPosition = scopeStart;

        public void SetEnd(int endPosition, TrackingToken tokenEnd)
        {
            if (startPosition <= endPosition)
            {
                actualEnd = tokenEnd.GetEnd(Snapshot);
                Scope = new TrackingBlock(Snapshot, startPosition, endPosition);
                Area = new TrackingBlock(Snapshot, actualStart, actualEnd);
            }
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
            actualStart <= point && actualEnd >= point;

        public virtual bool InRange(Span span) =>
            actualStart <= span.Start && actualEnd >= span.End;
    }
}
