﻿using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Parser
{
    class JoinedTextChange : ITextChange
    {

        public JoinedTextChange(INormalizedTextChangeCollection changes)
        {
            var oldStart = changes[0].OldSpan.Start;
            var oldEnd = changes[changes.Count - 1].OldEnd;
            OldSpan = new Span(oldStart, oldEnd - oldStart);
            Delta = changes[changes.Count - 1].NewEnd - changes[changes.Count - 1].OldEnd;
        }

        public Span OldSpan { get; }
        public int Delta { get; }

        public int LineCountDelta { get { throw new NotImplementedException(); } }
        public int NewEnd { get { throw new NotImplementedException(); } }
        public int NewLength { get { throw new NotImplementedException(); } }
        public int NewPosition { get { throw new NotImplementedException(); } }
        public Span NewSpan { get { throw new NotImplementedException(); } }
        public string NewText { get { throw new NotImplementedException(); } }
        public int OldEnd { get { throw new NotImplementedException(); } }
        public int OldLength { get { throw new NotImplementedException(); } }
        public int OldPosition { get { throw new NotImplementedException(); } }
        public string OldText { get { throw new NotImplementedException(); } }
    }

    public struct TrackingToken
    {
        public class NonOverlappingComparer : IComparer<TrackingToken>
        {
            public ITextSnapshot Version;

            public int Compare(TrackingToken x, TrackingToken y)
            {
                int result = x.Start.GetPosition(Version).CompareTo(y.Start.GetPosition(Version));
#if DEBUG
                //if (result == 0)
                //    System.Diagnostics.Debug.Assert(x.Length == y.Length);
#endif
                return result;
            }
        }

        public ITrackingPoint Start;
        public int Length;
        public int Type;

        public bool IsEmpty { get { return Start == null; } }
        public static TrackingToken Empty { get { return new TrackingToken(); } }

        internal TrackingToken(ITextSnapshot snapshot, TokenSpan arg) : this()
        {
            Start = snapshot.CreateTrackingPoint(arg.Span.Start, PointTrackingMode.Positive);
            Length = arg.Span.Length;
            Type = arg.Type;
        }

        public Span GetSpan(ITextSnapshot snap)
        {
            return new Span(Start.GetPosition(snap), Length);
        }

        public int GetStart(ITextSnapshot snap)
        {
            return Start.GetPosition(snap);
        }

        public int GetEnd(ITextSnapshot snap)
        {
            return Start.GetPosition(snap) + Length;
        }

        public string GetText(ITextSnapshot snap)
        {
            return snap.GetText(GetSpan(snap));
        }
    }

    internal class DocumentAnalysis
    {
        readonly TrackingToken.NonOverlappingComparer comparer = new TrackingToken.NonOverlappingComparer();
        SortedSet<TrackingToken> tree;
        readonly IRadAsmLexer lexer;
        public static readonly object Key = new object();

        public delegate void TokenChange(IList<TrackingToken> trackingTokens);
        public event TokenChange TokensChanged;

        public ITextSnapshot CurrentSnapshot
        {
            get { return comparer.Version; }
            set { comparer.Version = value; }
        }

        public DocumentAnalysis(IRadAsmLexer lexer, ITextSnapshot version)
        {
            this.lexer = lexer;
            CurrentSnapshot = version;
            Initialize();
        }

        public TrackingToken GetToken(int point)
        {
            return tree.GetCoveringToken(CurrentSnapshot, point);
        }

        public IEnumerable<TrackingToken> GetTokens(Span span)
        {
            if (span.Length == 0)
            {
                if (span.Start == 0 && span.End == 0)
                    return Enumerable.Empty<TrackingToken>();

                return new[] { GetToken(span.Start) };
            }
            return tree.GetCoveringTokens(CurrentSnapshot, span);
        }

        public void ApplyTextChanges(TextContentChangedEventArgs args)
        {
            try
            {
                ApplyTextChange(args.Before, args.After, new JoinedTextChange(args.Changes));
            }
            catch (Exception ex)
            {
                Error.ShowError(ex);
                Initialize();
                RaiseTokensChanged(tree.ToList());
            }
        }

        public void ApplyTextChange(ITextSnapshot before, ITextSnapshot after, ITextChange change)
        {
            List<TrackingToken> forRemoval = GetInvalidated(before, change);
            // Some of the tokens marked for removal must be deleted before applying a new version,
            // because otherwise some trackingtokens will have broken spans
            int i = 0;
            for (; i < forRemoval.Count; i++)
                tree.Remove(forRemoval[i]);
            CurrentSnapshot = after;
            IList<TrackingToken> updated = Rescan(forRemoval, before, change.Delta);
            for (; i < forRemoval.Count; i++)
                tree.Remove(forRemoval[i]);
            foreach (var token in updated)
                tree.Add(token);
            RaiseTokensChanged(updated);
            //CheckChanges(change);
        }

        private void RaiseTokensChanged(IList<TrackingToken> updated)
        {
            TokensChanged?.Invoke(updated);
        }

        private void Initialize()
        {
            tree = new SortedSet<TrackingToken>(lexer.Run(new string[] { CurrentSnapshot.GetText() }, 0).Select(t => new TrackingToken(CurrentSnapshot, t)), comparer);
        }

        private List<TrackingToken> GetInvalidated(ITextSnapshot oldSnapshot, ITextChange change)
        {
            return tree.GetInvalidatedBy(oldSnapshot, change.OldSpan);
        }

        private IList<TrackingToken> Rescan(List<TrackingToken> forRemoval, ITextSnapshot oldSnapshot, int delta)
        {
            Span invalidatedSpan = InvalidatedSpan(forRemoval, oldSnapshot, delta);
            string invalidatedText = CurrentSnapshot.GetText(invalidatedSpan);
            return RescanCore(forRemoval, invalidatedSpan, invalidatedText);
        }

        private IList<TrackingToken> RescanCore(List<TrackingToken> forRemoval, Span invalidatedSpan, string invalidatedText)
        {
            List<TrackingToken> newlyCreated = new List<TrackingToken>();
            List<TrackingToken> removalCandidates = new List<TrackingToken>();
            // this lazy iterator walks tokens that are outside of the initial invalidation span
            var debug1 = tree.Select(x => x.GetSpan(CurrentSnapshot)).ToArray();
            var debug2 = tree.InOrderAfter(CurrentSnapshot, invalidatedSpan.End).Select(x => x.GetSpan(CurrentSnapshot)).ToArray();
            var excessText = tree.InOrderAfter(CurrentSnapshot, invalidatedSpan.End)
                                 .Select(t => GetTextAndMarkForRemoval(t, ref removalCandidates))
                                 .TakeWhile(s => s != null);
            var tokens = lexer.Run(new string[] { invalidatedText }.Concat(excessText), invalidatedSpan.Start);
            foreach (var token in tokens)
            {
                newlyCreated.Add(new TrackingToken(CurrentSnapshot, token));
                if (token.Span.End == invalidatedSpan.End)
                    break;
                if (removalCandidates.Count > 0)
                {
                    if (token.Span.End == removalCandidates[removalCandidates.Count - 1].GetEnd(CurrentSnapshot))
                        break;
                }
            }
            AppendInvalidTokens(forRemoval, newlyCreated, removalCandidates);
            return newlyCreated;
        }

        private void AppendInvalidTokens(List<TrackingToken> forRemoval, List<TrackingToken> newlyCreated, List<TrackingToken> removalCandidates)
        {
            if (newlyCreated.Count == 0 || removalCandidates.Count == 0)
                return;
            int end = newlyCreated[newlyCreated.Count - 1].GetEnd(CurrentSnapshot);
            foreach (var token in removalCandidates)
            {
                int tokenEnd = token.GetEnd(CurrentSnapshot);
                if (tokenEnd > end)
                    break;
                forRemoval.Add(token);
                if (tokenEnd == end)
                    break;
            }
        }

        private string GetTextAndMarkForRemoval(TrackingToken current, ref List<TrackingToken> removalCandidates)
        {
            removalCandidates.Add(current);
            Span span = current.GetSpan(CurrentSnapshot);
            if (span.End > CurrentSnapshot.Length)
                return null;
            return current.GetText(CurrentSnapshot);
        }

        private Span InvalidatedSpan(IList<TrackingToken> invalid, ITextSnapshot oldSnapshot, int delta)
        {
            // if the set of invalidated tokens is empty, that means we
            // are observing text being inserted into an empty document
            if (invalid.Count == 0)
                return new Span(0, delta);
            int invalidationStart = invalid[0].GetStart(oldSnapshot); // this position is the same in both versions
            int invalidationEnd = GetInvalidationEnd(oldSnapshot, invalid, delta);
            return new Span(invalidationStart, invalidationEnd - invalidationStart);
        }

        private int GetInvalidationEnd(ITextSnapshot oldSnapshot, IList<TrackingToken> invalid, int delta)
        {
            var oldSpan = invalid[invalid.Count - 1].GetSpan(oldSnapshot);
            var newSpan = invalid[invalid.Count - 1].GetSpan(CurrentSnapshot);
            var tokenStartDelta = newSpan.Start - oldSpan.Start;
            return newSpan.End + delta - tokenStartDelta;
        }

        public static bool TryGet(ITextBuffer b, out DocumentAnalysis t)
        {
            if (!b.Properties.TryGetProperty<DocumentAnalysis>(Key, out var documentAnalysis))
            {
                t = null;
                return false;
            }

            t = documentAnalysis;
            return true;
        }

        public static DocumentAnalysis CreateAndRegister(IRadAsmLexer lexer, ITextBuffer buffer)
        {
            if (buffer.Properties.TryGetProperty<DocumentAnalysis>(Key, out var state))
                return state;

            state = new DocumentAnalysis(lexer, buffer.CurrentSnapshot);
            buffer.Changed += (src, arg) => state.ApplyTextChanges(arg);
            buffer.Properties.AddProperty(Key, state);
            return state;
        }
    }
}
