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
        IBlock Parrent { get; }
        ITextSnapshot Snapshot { get; }
        TrackingBlock Scope { get; }
        BlockType Type { get; }
        List<IBlock> Childrens { get; }
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
        public IBlock Parrent { get; }
        public ITextSnapshot Snapshot { get; }
        public BlockType Type { get; }
        public List<IBlock> Childrens { get; }
        public List<AnalysisToken> Tokens { get; }
        public TrackingBlock Scope { get; private set; }

        private int _actualStart;
        private int _actualEnd;

        private int startPosition;

        public Block(IBlock parrent, BlockType type, TrackingToken tokenStart)
        {
            Parrent = parrent;
            Type = type;
            Snapshot = parrent.Snapshot;
            _actualStart = tokenStart.GetStart(Snapshot);

            Childrens = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public Block(IBlock parrent, BlockType type, TrackingToken tokenStart, TrackingToken tokenEnd)
        {
            Parrent = parrent;
            Type = type;
            Snapshot = parrent.Snapshot;
            _actualStart = tokenStart.GetStart(Snapshot);
            _actualEnd = tokenEnd.GetEnd(Snapshot);
            Scope = new TrackingBlock(Snapshot, _actualStart, _actualEnd);

            Childrens = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public Block(ITextSnapshot snapshot)
        {
            Parrent = null;
            Type = BlockType.Root;
            Snapshot = snapshot;
            _actualStart = 0;
            _actualEnd = Snapshot.Length - 1;

            Childrens = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public void SetStart(int scopeStart) =>
            startPosition = scopeStart;

        public void SetEnd(int endPosition, TrackingToken tokenEnd)
        {
            SetScopeEnd(endPosition);
            _actualEnd = tokenEnd.GetEnd(Snapshot);
        }

        public void AddChildren(IBlock block) =>
            Childrens.Add(block);

        public void AddToken(AnalysisToken token) =>
            Tokens.Add(token);

        public bool InScope(int point) =>
            Scope.GetSpan(Snapshot).Contains(point);

        public bool InScope(Span span) =>
            Scope.GetSpan(Snapshot).Contains(span);

        public bool InRange(int point) =>
            _actualStart <= point && _actualEnd >= point;

        public bool InRange(Span span) =>
            _actualStart <= span.Start && _actualEnd >= span.End;

        private void SetScopeEnd(int endPosition)
        {
            if (startPosition < endPosition)
                Scope = new TrackingBlock(Snapshot, startPosition, endPosition);
        }
    }
}
