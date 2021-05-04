using System.Collections.Generic;
using System.Windows.Documents;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.IntelliSense.FindReferences.Entries
{
    internal class DocumentSpanEntry : Entry
    {
        private readonly SnapshotSpan _span;
        private readonly ITextSnapshotLine _line;

        public DocumentSpanEntry(DefinitionBucket definitionBucket, SnapshotSpan span)
            : base(definitionBucket)
        {
            _span = span;
            _line = span.Start.GetContainingLine();
        }

        public override IList<Inline> CreateLineTextInlines()
        {
            //TODO: classify text
            return null;
        }

        protected override object GetValueWorker(string keyName)
        {
            switch (keyName)
            {
                case StandardTableKeyNames.ProjectGuid: return null;
                case StandardTableKeyNames.ProjectName: return null;
                case StandardTableKeyNames.DocumentName: return null;
                case StandardTableKeyNames.Line: return _line.LineNumber + 1;
                case StandardTableKeyNames.Column: return _span.Start - _line.Start;
                case StandardTableKeyNames.Text: return _line.GetText();
                default: return null;
            }
        }
    }
}
