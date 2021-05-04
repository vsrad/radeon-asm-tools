using System.Collections.Immutable;
using System.Windows;
using Microsoft.VisualStudio.Shell.TableControl;
using VSRAD.Syntax.IntelliSense.FindReferences.Entries;

namespace VSRAD.Syntax.IntelliSense.FindReferences
{
    internal class TableEntriesSnapshot : WpfTableEntriesSnapshotBase
    {
        private const string SelfKeyName = "self";
        private readonly ImmutableList<Entry> _entries;

        public override int VersionNumber { get; }
        public override int Count => _entries.Count;

        public TableEntriesSnapshot(ImmutableList<Entry> entries, int versionNumber)
        {
            _entries = entries;
            VersionNumber = versionNumber;
        }

        public override bool TryGetValue(int index, string keyName, out object content)
        {
            if (keyName == SelfKeyName)
            {
                content = _entries[index];
                return true;
            }

            return _entries[index].TryGetValue(keyName, out content);
        }

        public override bool TryCreateColumnContent(int index, string columnName, bool singleColumnView, out FrameworkElement content) =>
            _entries[index].TryCreateColumnContent(columnName, out content);
    }
}
