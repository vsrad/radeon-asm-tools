using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.Parser.Helper;
using VSRAD.Syntax.Parser.Blocks;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using VSRAD.Syntax.Options;
using System.ComponentModel.Composition;
using System.Threading;

namespace VSRAD.Syntax.Parser
{
    [Export]
    internal class DocumentAnalysisProvoder
    {
        private readonly InstructionListManager _instructionListManager;

        [ImportingConstructor]
        public DocumentAnalysisProvoder(InstructionListManager instructionListManager)
        {
            _instructionListManager = instructionListManager;
        }

        public DocumentAnalysis CreateDocumentAnalysis(ITextBuffer buffer)
        {
            if (buffer.CurrentSnapshot.IsRadeonAsm2ContentType())
                return buffer.Properties.GetOrCreateSingletonProperty(() => new DocumentAnalysis(new Asm2Lexer(), new Asm2Parser(), buffer, _instructionListManager));
            else
                return buffer.Properties.GetOrCreateSingletonProperty(() => new DocumentAnalysis(new AsmLexer(), new AsmParser(), buffer, _instructionListManager));
        }
    }

    internal class DocumentAnalysis
    {
        private readonly TrackingToken.NonOverlappingComparer _comparer;
        private readonly ILexer _lexer;
        private readonly IParser _parser;
        private CancellationTokenSource parserCts;

        public delegate void TokenChange(IList<TrackingToken> trackingTokens);
        public event TokenChange TokensChanged;

        public delegate void ParserUpdate(ITextSnapshot version, IReadOnlyList<IBlock> blocks);
        public event ParserUpdate ParserUpdated;

        public Helper.SortedSet<TrackingToken> LastLexerResult;
        public IReadOnlyList<IBlock> LastParserResult;

        public int IDENTIFIER => _lexer.IdentifierIdx;
        public int LINE_COMMENT => _lexer.LineCommentIdx;
        public int BLOCK_COMMENT => _lexer.BlockCommentIdx;

        public ITextSnapshot CurrentSnapshot
        {
            get { return _comparer.Version; }
            set { _comparer.Version = value; }
        }

        public DocumentAnalysis(ILexer lexer, IParser parser, ITextBuffer buffer, InstructionListManager instructionListManager)
        {
            _lexer = lexer;
            _parser = parser;
            _comparer = new TrackingToken.NonOverlappingComparer();
            parserCts = new CancellationTokenSource();
            CurrentSnapshot = buffer.CurrentSnapshot;
            LastParserResult = new List<IBlock>();

            buffer.Changed += BufferChanged;
            instructionListManager.InstructionUpdated += InstructionListUpdated;
            _parser.UpdateInstructionSet(instructionListManager.InstructionList);
            Initialize();
        }

        public TrackingToken GetToken(int point) =>
            LastLexerResult.GetCoveringToken(CurrentSnapshot, point);

        public IEnumerable<TrackingToken> GetTokens(Span span)
        {
            if (span.Length == 0)
            {
                if (span.Start == 0 && span.End == 0)
                    return Enumerable.Empty<TrackingToken>();

                return new[] { GetToken(span.Start) };
            }
            return LastLexerResult.GetCoveringTokens(CurrentSnapshot, span);
        }

        public RadAsmTokenType LexerTokenToRadAsmToken(int type) =>
            _lexer.LexerTokenToRadAsmToken(type);

        private void BufferChanged(object src, TextContentChangedEventArgs arg) =>
            ThreadHelper.JoinableTaskFactory.RunAsync(() => ApplyTextChangesAsync(arg));

        private void InstructionListUpdated(IReadOnlyList<string> instructions)
        {
            _parser.UpdateInstructionSet(instructions);
            LastLexerResult.UpdateVersion();
            RunParser();
        }

        private Task ApplyTextChangesAsync(TextContentChangedEventArgs args)
        {
            try
            {
                parserCts.Cancel();
                ApplyTextChange(args.Before, args.After, new JoinedTextChange(args.Changes));
            }
            catch (Exception ex)
            {
                Error.ShowError(ex);
                Initialize();
            }
            return Task.CompletedTask;
        }

        private void ApplyTextChange(ITextSnapshot before, ITextSnapshot after, ITextChange change)
        {
            List<TrackingToken> forRemoval = GetInvalidated(before, change);
            // Some of the tokens marked for removal must be deleted before applying a new version,
            // because otherwise some trackingtokens will have broken spans
            int i = 0;
            for (; i < forRemoval.Count; i++)
                LastLexerResult.Remove(forRemoval[i]);
            CurrentSnapshot = after;
            IList<TrackingToken> updated = Rescan(forRemoval, before, change.Delta);
            for (; i < forRemoval.Count; i++)
                LastLexerResult.Remove(forRemoval[i]);
            foreach (var token in updated)
                LastLexerResult.Add(token);
            RaiseTokensChanged(updated);
        }

        private void RaiseTokensChanged(IList<TrackingToken> updated)
        {
            RunParser();
            TokensChanged?.Invoke(updated);
        }

        private void Initialize()
        {
            LastLexerResult = new Helper.SortedSet<TrackingToken>(_lexer.Run(new string[] { CurrentSnapshot.GetText() }, 0).Select(t => new TrackingToken(CurrentSnapshot, t)), _comparer);
            RaiseTokensChanged(LastLexerResult.ToList());
        }

        private void RunParser()
        {
            parserCts = new CancellationTokenSource();
            ThreadPool.QueueUserWorkItem(RunParser, new object[] { LastLexerResult.Version, LastLexerResult, CurrentSnapshot, parserCts });
        }

        private void RunParser(object state)
        {
            var arrayParams = state as object[];
            var lexerVersion = (int)arrayParams[0];
            var tokens = arrayParams[1] as IEnumerable<TrackingToken>;
            var version = arrayParams[2] as ITextSnapshot;
            var cts = arrayParams[3] as CancellationTokenSource;

            try
            {
                var parserResult = _parser.Run(lexerVersion, tokens, CurrentSnapshot, cts.Token);
                if (parserResult != null)
                {
                    LastParserResult = parserResult;
                    ParserUpdated?.Invoke(version, LastParserResult);
                }
            }
            catch (Exception) { }
        }

        private List<TrackingToken> GetInvalidated(ITextSnapshot oldSnapshot, ITextChange change) =>
            LastLexerResult.GetInvalidatedBy(oldSnapshot, change.OldSpan);

        private IList<TrackingToken> Rescan(List<TrackingToken> forRemoval, ITextSnapshot oldSnapshot, int delta)
        {
            var invalidatedSpan = InvalidatedSpan(forRemoval, oldSnapshot, delta);
            var invalidatedText = CurrentSnapshot.GetText(invalidatedSpan);

            return RescanCore(forRemoval, invalidatedSpan, invalidatedText);
        }

        private IList<TrackingToken> RescanCore(List<TrackingToken> forRemoval, Span invalidatedSpan, string invalidatedText)
        {
            var newlyCreated = new List<TrackingToken>();
            var removalCandidates = new List<TrackingToken>();
            // this lazy iterator walks tokens that are outside of the initial invalidation span
            var excessText = LastLexerResult.InOrderAfter(CurrentSnapshot, invalidatedSpan.End)
                                 .Select(t => GetTextAndMarkForRemoval(t, ref removalCandidates))
                                 .TakeWhile(s => s != null);
            var tokens = _lexer.Run(new string[] { invalidatedText }.Concat(excessText), invalidatedSpan.Start);
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
            var span = current.GetSpan(CurrentSnapshot);

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

        private class JoinedTextChange : ITextChange
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
    }
}
