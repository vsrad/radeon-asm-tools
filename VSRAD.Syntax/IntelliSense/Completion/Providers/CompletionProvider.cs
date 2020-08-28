using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class CompletionContext
    {
        public static CompletionContext Empty => new CompletionContext(new List<CompletionItem>());
        public List<CompletionItem> Items;

        public CompletionContext(List<CompletionItem> items)
        {
            Items = items;
        }
    }

    internal class CompletionItem
    {
        public string Text { get; }
        public List<NavigationToken> Tokens { get; }
        public ImageElement ImageElement { get; }

        public CompletionItem(string text, ImageElement imageElement, List<NavigationToken> navigationTokens)
        {
            Text = text;
            ImageElement = imageElement;
            Tokens = navigationTokens;
        }

        public CompletionItem(string text, ImageElement imageElement, NavigationToken navigationToken)
        {
            Text = text;
            ImageElement = imageElement;
            Tokens = new List<NavigationToken>() { navigationToken };
        }
    }
    internal abstract class CompletionProvider
    {
        public CompletionProvider(OptionsProvider optionsProvider)
        {
            optionsProvider.OptionsUpdated += DisplayOptionsUpdated;
        }

        public abstract Task<CompletionContext> GetContextAsync(DocumentAnalysis documentAnalysis, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan);

        public abstract void DisplayOptionsUpdated(OptionsProvider sender);

        public static ImageElement GetImageElement(int imageId) =>
            new ImageElement(new ImageId(ImageCatalogGuid, imageId));

        private static readonly Guid ImageCatalogGuid = Guid.Parse(/* image catalog guid */ "ae27a6b0-e345-4288-96df-5eaf394ee369");
    }
}
