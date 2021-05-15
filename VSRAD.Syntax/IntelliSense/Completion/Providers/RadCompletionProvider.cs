using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;
using System.Threading;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class RadCompletionContext
    {
        public static RadCompletionContext Empty => new RadCompletionContext(new List<ICompletionItem>());
        public IReadOnlyList<ICompletionItem> Items;

        public RadCompletionContext(IReadOnlyList<ICompletionItem> items)
        {
            Items = items;
        }
    }

    internal abstract class RadCompletionProvider
    {
        protected RadCompletionProvider(GeneralOptionProvider generalOptionProvider)
        {
            generalOptionProvider.OptionsUpdated += DisplayOptionsUpdated;
        }

        public abstract Task<RadCompletionContext> GetContextAsync(IDocument document, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken);

        public abstract void DisplayOptionsUpdated(GeneralOptionProvider sender);
    }
}
