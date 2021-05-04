using System.Collections.Generic;
using System.Windows.Documents;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.IntelliSense.FindReferences.Entries;
using ShellDefinitionBucket = Microsoft.VisualStudio.Shell.FindAllReferences.DefinitionBucket;

namespace VSRAD.Syntax.IntelliSense.FindReferences
{
    internal class DefinitionBucket : ShellDefinitionBucket, IInlinedEntry
    {
        private readonly IDefinitionToken _token;
        private readonly Dictionary<(string filePath, SnapshotSpan span), DocumentSpanEntry> _locationToEntry;

        public DefinitionBucket(IDefinitionToken definitionToken, string name, string sourceTypeIdentifier, string identifier) 
            : base(name, sourceTypeIdentifier, identifier)
        {
            _token = definitionToken;
            _locationToEntry = new Dictionary<(string filePath, SnapshotSpan span), DocumentSpanEntry>();
        }

        public override bool TryGetValue(string key, out object content)
        {
            content = GetValue(key);
            return content != null;
        }

        private object GetValue(string key)
        {
            switch (key)
            {
                case StandardTableKeyNames.Text:
                case StandardTableKeyNames.FullText:
                    return _token.Span.GetText();
                case StandardTableKeyNames2.TextInlines:
                    return CreateLineTextInlines();
                case StandardTableKeyNames2.DefinitionIcon:
                    return GetDefinitionImageMoniker();
                default: return null;
            }
        }

        public DocumentSpanEntry GetOrAddEntry(string filePath, SnapshotSpan span, DocumentSpanEntry entry)
        {
            var key = (filePath, span);
            lock (_locationToEntry)
            {
                if (_locationToEntry.TryGetValue(key, out var spanEntry))
                {
                    return spanEntry;
                }

                _locationToEntry.Add(key, entry);
                return entry;
            }
        }
        public IList<Inline> CreateLineTextInlines()
        {
            // TODO: classify text
            return null;
        }

        public ImageMoniker GetDefinitionImageMoniker()
        {
            // TODO: use definition image moniker
            return KnownMonikers.UserFunction;
        }
    }
}
