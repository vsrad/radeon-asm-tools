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
        Span Scope { get; }
        Span Area { get; }
        BlockType Type { get; }
        List<IBlock> Children { get; }
        List<IAnalysisToken> Tokens { get; }

        void AddChildren(IBlock block);
        void AddToken(IAnalysisToken token);
        bool InScope(int point);
        bool InScope(Span span);
        bool InRange(int point);
        bool InRange(Span span);
    }

    internal class Block : IBlock
    {
        public IBlock Parent { get; }
        public BlockType Type { get; }
        public List<IBlock> Children { get; }
        public List<IAnalysisToken> Tokens { get; }
        public Span Scope { get; private set; }
        public Span Area { get; private set; }

        private int _startPosition;

        public Block(IBlock parent, BlockType type, TrackingToken start, ITextSnapshot snapshot)
            : this(parent, type, start.GetStart(snapshot), snapshot.Length - 1) { }

        public Block(IBlock parent, BlockType type, TrackingToken start, TrackingToken end, ITextSnapshot snapshot)
            : this(parent, type, start.GetStart(snapshot), end.GetEnd(snapshot)) { }

        public Block(IBlock parent, BlockType type, int start, int end)
        {
            Parent = parent;
            Type = type;

            Children = new List<IBlock>();
            Tokens = new List<IAnalysisToken>();

            Scope = new Span(start, end - start);
            Area = Scope;
        }

        public Block()
        {
            Parent = null;
            Type = BlockType.Root;

            Children = new List<IBlock>();
            Tokens = new List<IAnalysisToken>();
        }

        public void SetStart(int scopeStart) =>
            _startPosition = scopeStart;

        public void SetEnd(int endPosition, TrackingToken endToken, ITextSnapshot snapshot)
        {
            if (_startPosition > endPosition) return;

            Scope = new Span(_startPosition, endPosition - _startPosition);
            Area = new Span(Area.Start, endToken.GetEnd(snapshot) - Area.Start);
        }

        public virtual void AddChildren(IBlock block) =>
            Children.Add(block);

        public virtual void AddToken(IAnalysisToken token) =>
            Tokens.Add(token);

        public virtual bool InScope(int point) =>
            Scope.Contains(point);

        public virtual bool InScope(Span span) =>
            Scope.Contains(span);

        public virtual bool InRange(int point) =>
            Area.Contains(point);

        public virtual bool InRange(Span span) =>
            Area.Contains(span);
    }
}
