using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.IntelliSense.FindReferences.Entries;
using VSRAD.Syntax.IntelliSense.Navigation;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.IntelliSense.FindReferences
{

    internal class TableDataSourceFindReferencesContext : ITableDataSource, ITableEntriesSnapshotFactory
    {
        private readonly object _lock = new object();

        public const string RadeonAsmFindUsagesTableDataSourceIdentifier =
            nameof(RadeonAsmFindUsagesTableDataSourceIdentifier);

        public const string RadeonAsmFindUsagesTableDataSourceSourceTypeIdentifier =
            nameof(RadeonAsmFindUsagesTableDataSourceSourceTypeIdentifier);

        public string Identifier => RadeonAsmFindUsagesTableDataSourceIdentifier;
        public string SourceTypeIdentifier => RadeonAsmFindUsagesTableDataSourceSourceTypeIdentifier;
        public string DisplayName => RadeonAsmFindUsagesTableDataSourceIdentifier;
        public int CurrentVersionNumber { get; private set; }
        public CancellationTokenSource CancellationTokenSource { get; private set; }

        private readonly Dictionary<IDefinitionToken, DefinitionBucket> _definitionBuckets =
            new Dictionary<IDefinitionToken, DefinitionBucket>();
        private readonly IFindAllReferencesWindow _findReferencesWindow;
        private readonly FindReferencesPresenter _presenter;
        private readonly SnapshotPoint _triggerPoint;
        private readonly IDocumentFactory _documentFactory;
        private ITableDataSink _tableDataSink;
        private ITableEntriesSnapshot _lastSnapshot;
        private ImmutableList<Entry> _entries;

        public TableDataSourceFindReferencesContext(FindReferencesPresenter presenter, IDocumentFactory documentFactory,
            SnapshotPoint triggerPoint, IFindAllReferencesWindow findReferencesWindow)
        {
            _triggerPoint = triggerPoint;
            _presenter = presenter;
            _documentFactory = documentFactory;
            _findReferencesWindow = findReferencesWindow;
            _entries = ImmutableList<Entry>.Empty;
            CancellationTokenSource = new CancellationTokenSource();

            _presenter.Add(this);
            _findReferencesWindow.Manager.AddSource(this, SelectCustomColumnsToInclude());
            _findReferencesWindow.Closed += FindReferencesWindowClosed;

            _ = Task.Run(StartSearchAsync, CancellationTokenSource.Token);
        }

        public IDisposable Subscribe(ITableDataSink sink)
        {
            _tableDataSink = sink;
            _tableDataSink.AddFactory(this, true);
            _tableDataSink.IsStable = true;
            return this;
        }

        public ITableEntriesSnapshot GetCurrentSnapshot()
        {
            lock (_lock)
            {
                if (_lastSnapshot?.VersionNumber != CurrentVersionNumber)
                    _lastSnapshot = new TableEntriesSnapshot(_entries, CurrentVersionNumber);

                return _lastSnapshot;
            }
        }

        public ITableEntriesSnapshot GetSnapshot(int versionNumber)
        {
            lock (_lock)
            {
                if (_lastSnapshot?.VersionNumber == versionNumber)
                    return _lastSnapshot;

                if (versionNumber == CurrentVersionNumber)
                    return GetCurrentSnapshot();
            }

            NotifyChange();
            return null;
        }

        private static IReadOnlyCollection<string> SelectCustomColumnsToInclude() =>
            new List<string>
            {
                StandardTableKeyNames.Text,
                StandardTableKeyNames.DocumentName,
                StandardTableKeyNames.Line,
                StandardTableKeyNames.Column
            };

        private void CancelSearch()
        {
            if (CancellationTokenSource == null)
                return;

            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = null;
        }

        private async Task StartSearchAsync()
        {
            var ct = CancellationTokenSource.Token;
            ct.ThrowIfCancellationRequested();
            var navigationResult = await _presenter.NavigationTokenService.GetNavigationsAsync(_triggerPoint);

            if (navigationResult.Values.Count == 0)
                return;

            AddDeclarationEntries(navigationResult, ct);
        }

        private void AddDeclarationEntries(NavigationTokenServiceResult navigations, CancellationToken cancellationToken)
        {
            var newEntries = new List<Entry>();
            foreach (var navigation in navigations.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var definition = navigation.Definition;
                if (_definitionBuckets.ContainsKey(definition))
                    continue;

                var definitionText = definition.GetText();
                var bucket = new DefinitionBucket(definition, 
                    definitionText, "Test Tye Identifier", "Test Identifier");
                _definitionBuckets.Add(definition, bucket);

                foreach (var reference in definition.References)
                {
                    var span = reference.Span;

                    var document = _documentFactory.GetOrCreateDocument(span.Snapshot.TextBuffer);
                    if (document == null) return;

                    var entry = new DocumentSpanEntry(bucket, span);
                    var newEntry = bucket.GetOrAddEntry(document.Path, span, entry);

                    newEntries.Add(newEntry);
                }
            }

            lock (_lock)
            {
                _entries = _entries.AddRange(newEntries);
                CurrentVersionNumber++;
            }

            NotifyChange();
        }

        private void FindReferencesWindowClosed(object sender, EventArgs e)
        {
            _findReferencesWindow.Closed -= FindReferencesWindowClosed;
            CancelSearch();
        }

        protected void NotifyChange()
            => _tableDataSink.FactorySnapshotChanged(this);

        public void Dispose()
        {
            CancelSearch();
            _findReferencesWindow.Manager.RemoveSource(this);
            _presenter.Remove(this);
        }
    }
}
