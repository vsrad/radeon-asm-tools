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
        TrackingBlock Scope { get; }
        BlockType Type { get; }
        TrackingToken TokenStart { get; }
        TrackingToken TokenEnd { get; }
        List<IBlock> Childrens { get; }
        List<AnalysisToken> Tokens { get; }

        void AddToken(RadAsmTokenType tokenType, TrackingToken trackingToken);
        void AddChildren(IBlock block);
        bool InScope(ITextSnapshot version, int point);
        bool InScope(ITextSnapshot version, Span span);
        void SetEnd(ITextSnapshot version, int endPosition, TrackingToken tokenEnd);
        void SetScopeStart(int startPosition);
    }

    internal class Block : IBlock
    {
        public static IBlock Empty() => new Block(null, BlockType.Root, TrackingToken.Empty);

        public IBlock Parrent { get; }
        public BlockType Type { get; }
        public TrackingToken TokenStart { get; }
        public TrackingToken TokenEnd { get; private set; }
        public List<IBlock> Childrens { get; }
        public List<AnalysisToken> Tokens { get; }
        public TrackingBlock Scope { get; private set; }

        private int startPosition;

        public Block(IBlock parrent, BlockType type, TrackingToken tokenStart)
        {
            Parrent = parrent;
            Type = type;
            TokenStart = tokenStart;

            Childrens = new List<IBlock>();
            Tokens = new List<AnalysisToken>();
        }

        public void SetScope(ITextSnapshot version, Span span) =>
            Scope = new TrackingBlock(version, span);

        public void SetScopeStart(int scopeStart) =>
            startPosition = scopeStart;

        public void SetEnd(ITextSnapshot version, int endPosition, TrackingToken tokenEnd)
        {
            SetScopeEnd(version, endPosition);
            TokenEnd = tokenEnd;
        }

        public void AddChildren(IBlock block) =>
            Childrens.Add(block);

        public void AddToken(RadAsmTokenType tokenType, TrackingToken trackingToken) =>
            Tokens.Add(new AnalysisToken(tokenType, trackingToken));

        public bool InScope(ITextSnapshot version, int point) =>
            Scope.GetSpan(version).Contains(point);

        public bool InScope(ITextSnapshot version, Span span) =>
            Scope.GetSpan(version).Contains(span);

        private void SetScopeEnd(ITextSnapshot version, int endPosition)
        {
            if (startPosition < endPosition)
                Scope = new TrackingBlock(version, startPosition, endPosition);
        }
    }
}
