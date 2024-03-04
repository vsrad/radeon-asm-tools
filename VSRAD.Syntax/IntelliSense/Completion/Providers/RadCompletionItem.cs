using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text.Adornments;
using System;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    public readonly struct RadCompletionItem : IEquatable<RadCompletionItem>
    {
        public IntelliSenseInfo Info { get; }
        public ImageElement ImageElement { get; }

        public RadCompletionItem(IntelliSenseInfo info, ImageElement imageElement)
        {
            Info = info;
            ImageElement = imageElement;
        }

        public CompletionItem CreateVsCompletionItem(IAsyncCompletionSource asyncCompletionSource)
        {
            var vsCompletionItem = new CompletionItem(Info.Symbol, asyncCompletionSource, ImageElement);
            vsCompletionItem.Properties.AddProperty(typeof(RadCompletionItem), this);
            return vsCompletionItem;
        }

        public static RadCompletionItem GetFromVsCompletionItem(CompletionItem vsCompletionItem) =>
            vsCompletionItem.Properties.GetProperty<RadCompletionItem>(typeof(RadCompletionItem));

        public bool Equals(RadCompletionItem o) => Info == o.Info && ImageElement == o.ImageElement;

        public static bool operator ==(RadCompletionItem left, RadCompletionItem right) => left.Equals(right);

        public static bool operator !=(RadCompletionItem left, RadCompletionItem right) => !(left == right);

        public override bool Equals(object obj) => obj is RadCompletionItem o && Equals(o);

        public override int GetHashCode() => (Info, ImageElement).GetHashCode();
    }
}
