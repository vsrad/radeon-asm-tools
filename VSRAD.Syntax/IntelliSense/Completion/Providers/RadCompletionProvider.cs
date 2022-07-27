using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
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
        public RadCompletionProvider(OptionsProvider optionsProvider)
        {
            optionsProvider.OptionsUpdated += DisplayOptionsUpdated;
        }

        public abstract Task<RadCompletionContext> GetContextAsync(IDocument document, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken);

        public abstract void DisplayOptionsUpdated(OptionsProvider sender);

        public static ImageElement GetImageElement(int imageId) =>
            new ImageElement(new ImageId(ImageCatalogGuid, imageId));

        private static readonly Guid ImageCatalogGuid = Guid.Parse(/* image catalog guid */ "ae27a6b0-e345-4288-96df-5eaf394ee369");
    }
}
