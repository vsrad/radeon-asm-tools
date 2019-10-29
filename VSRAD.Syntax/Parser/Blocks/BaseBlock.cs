using VSRAD.Syntax.Parser.Tokens;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Parser.Blocks
{
    public class BaseBlock : IBaseBlock
    {
        public BaseBlock(IBaseBlock parrent, BlockType blockType, SnapshotPoint blockStart, SnapshotPoint blockActualStart = default)
        {
            this.Parrent = parrent;
            this.BlockType = blockType;
            this.BlockStart = blockStart;
            this.BlockActualStart = (blockActualStart != default) ? blockActualStart : blockStart;
            this.Children = new List<IBaseBlock>();
            this.Tokens = new List<IBaseToken>();
        }

        public bool BlockReady { get; protected set; } = false;
        public SnapshotSpan BlockSpan { get; protected set; }
        public IList<IBaseBlock> Children { get; }
        public virtual IList<IBaseToken> Tokens { get; }
        public IBaseBlock Parrent { get; }
        public BlockType BlockType { get; private set; }

        private SnapshotPoint BlockStart { get; set; }
        private SnapshotPoint BlockActualStart { get; set; }

        public SnapshotSpan BlockActualSpan { get; set; }

        public IBaseBlock AddChildren(BlockType blockType, SnapshotPoint startBlock, SnapshotPoint actualStart = default)
        {
            var children = new BaseBlock(this, blockType, startBlock, actualStart);
            Children.Add(children);
            return children;
        }

        public IBaseBlock AddChildren(IBaseBlock baseBlock)
        {
            Children.Add(baseBlock);
            return baseBlock;
        }

        public IBaseToken AddToken(SnapshotSpan symbolSpan, TokenType tokenType)
        {
            var token = new BaseToken(symbolSpan, tokenType);
            this.Tokens.Add(token);
            return token;
        }

        public IList<IBaseToken> GetTokens()
        {
            var listElements = new List<IBaseToken>();
            foreach (var codeBlockChild in Children)
            {
                listElements.AddRange(codeBlockChild.GetTokens());
            }
            if (this.BlockReady)
                listElements.AddRange(this.Tokens);
            return listElements;
        }

        public IList<IBaseBlock> GetBlockElements()
        {
            var listElements = new List<IBaseBlock>();
            foreach (var codeBlockChild in Children.ToList())
            {
                listElements.AddRange(codeBlockChild.GetBlockElements());
            }
            if (this.BlockReady)
                listElements.Add(this);
            return listElements;
        }

        public IList<SnapshotSpan> GetBlocksSnapshotSpan()
        {
            var listElements = new List<SnapshotSpan>();
            foreach (var codeBlockChild in Children)
            {
                listElements.AddRange(codeBlockChild.GetBlocksSnapshotSpan());
            }
            if (this.BlockReady)
                listElements.Add(this.BlockSpan);
            return listElements;
        }

        public IList<Span> GetBlocksSpan()
        {
            var listElements = new List<Span>();
            foreach (var codeBlockChild in Children)
            {
                listElements.AddRange(codeBlockChild.GetBlocksSpan());
            }
            if (this.BlockReady)
                listElements.Add(this.BlockSpan);
            return listElements;
        }

        public virtual void SetBlockReady(SnapshotPoint endBlock)
        {
            if (this.BlockStart.Snapshot != null && this.BlockStart.Snapshot.Equals(endBlock.Snapshot) && this.BlockStart < endBlock)
            {
                this.BlockSpan = new SnapshotSpan(this.BlockStart, endBlock);
                SetActualSpan();
                this.BlockReady = true;
            }
        }

        public virtual void SetActualSpan()
        {
            this.BlockActualSpan = new SnapshotSpan(BlockActualStart, BlockSpan.End);
        }
    }
}
