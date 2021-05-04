using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Microsoft.VisualStudio.Shell.TableControl;

namespace VSRAD.Syntax.IntelliSense.FindReferences.Entries
{
    internal abstract class Entry : IInlinedEntry
    {
        public readonly DefinitionBucket DefinitionBucket;

        protected Entry(DefinitionBucket definitionBucket)
        {
            DefinitionBucket = definitionBucket;
        }

        public bool TryGetValue(string keyName, out object content)
        {
            content = GetValue(keyName);
            return content != null;
        }

        private object GetValue(string keyName)
        {
            switch (keyName)
            {
                case StandardTableKeyNames2.LineText:
                    return CreateLineTextInlines();
                case StandardTableKeyNames2.Definition:
                    return DefinitionBucket;
                case StandardTableKeyNames2.DefinitionIcon:
                    return DefinitionBucket.GetDefinitionImageMoniker();
            }

            return GetValueWorker(keyName);
        }

        public abstract IList<Inline> CreateLineTextInlines();

        public virtual bool TryCreateColumnContent(string columnName, out FrameworkElement content)
        {
            content = null;
            return false;
        }

        protected abstract object GetValueWorker(string keyName);
    }
}
