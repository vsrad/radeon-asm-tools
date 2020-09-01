using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core.Helper
{
    public class TokenizerCollection : SortedSet<TrackingToken>, ITokenizerCollection<TrackingToken>
    {
        public TokenizerCollection(IEnumerable<TrackingToken> collection, IComparer<TrackingToken> comparer)
            : base(collection, comparer) { }

        public List<TrackingToken> GetInvalidated(ITextSnapshot version, Span span)
        {
            List<TrackingToken> tokens = new List<TrackingToken>();
            if (Root != null)
            {
                FillInvalidatedTokens(Root, version, span, tokens);
                if (tokens.Count == 0)
                    tokens.Add(Min);
            }
            return tokens;
        }

        public IEnumerable<TrackingToken> InOrderAfter(ITextSnapshot version, int start)
        {
            // search first
            Node current = Root;
            if (this.Root == null)
                yield break;
            Stack<Node> stack = new Stack<Node>(2 * (log2(Count + 1)));
            // find exact
            while (true)
            {
                if (current == null)
                    yield break;
                Span currentSpan = current.Item.GetSpan(version);
                if (currentSpan.Contains(start))
                {
                    if (currentSpan.Start == start)
                        yield return current.Item;
                    break;
                }
                else if (start < currentSpan.Start)
                {
                    if (current.Left == null)
                    {
                        yield return current.Item;
                        break;
                    }
                    stack.Push(current);
                    current = current.Left;
                }
                else
                {
                    stack.Push(current);
                    current = current.Right;
                }
            }
            // yield next
            while (true)
            {
                current = Next(current, stack);
                if (current == null)
                    break;
                yield return current.Item;
            }
        }

        public TrackingToken GetCoveringToken(ITextSnapshot version, int pos)
        {
            SortedSet<TrackingToken>.Node current = Root;
            while (current != null)
            {
                Span span = current.Item.GetSpan(version);
                if (span.Contains(pos))
                {
                    return current.Item;
                }
                else
                {
                    current = (pos < span.Start) ? current.Left : current.Right;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(pos));
        }

        public IEnumerable<TrackingToken> GetCoveringTokens(ITextSnapshot version, Span span)
        {
            List<TrackingToken> tokens = new List<TrackingToken>();
            if (Root != null)
                FillCoveringTokens(Root, version, span, tokens);
            return tokens;
        }

        private static void FillCoveringTokens(Node current, ITextSnapshot version, Span span, List<TrackingToken> tokens)
        {
            var currentSpan = current.Item.GetSpan(version);
            if (current.Left != null && span.Start < currentSpan.Start)
                FillCoveringTokens(current.Left, version, span, tokens);
            if (currentSpan.OverlapsWith(span))
                tokens.Add(current.Item);
            if (current.Right != null && span.End > currentSpan.End)
                FillCoveringTokens(current.Right, version, span, tokens);
        }

        private static void FillInvalidatedTokens(Node current, ITextSnapshot version, Span span, List<TrackingToken> tokens)
        {
            var currentSpan = current.Item.GetSpan(version);
            if (current.Left != null && span.Start <= currentSpan.Start)
                FillInvalidatedTokens(current.Left, version, span, tokens);
            if (RightInclusiveOverlap(currentSpan, span))
                tokens.Add(current.Item);
            if (current.Right != null && span.End >= currentSpan.End)
                FillInvalidatedTokens(current.Right, version, span, tokens);
        }

        private static bool RightInclusiveOverlap(Span current, Span span)
        {
            if (span.End >= current.End)
                return span.Start <= current.End;
            if (span.Start <= current.Start)
                return span.End > current.Start;
            return true;
        }

        private static Node Next(Node current, Stack<Node> parents)
        {
            if (current.Right != null)
            {
                parents.Push(current);
                return Minimum(current.Right, parents);
            }
            return PopUntilLeftChild(current, parents);
        }

        private static Node Minimum(Node current, Stack<Node> parents)
        {
            while (current.Left != null)
            {
                parents.Push(current);
                current = current.Left;
            }
            return current;
        }

        private static Node PopUntilLeftChild(Node current, Stack<Node> parents)
        {
            while (parents.Count > 0)
            {
                var parent = parents.Pop();
                if (current == parent.Left)
                    return parent;
                current = parent;
            }
            return null;
        }
    }
}
