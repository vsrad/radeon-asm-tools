using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Adornments;
using System.Threading;
using System.Threading.Tasks;
using VsComplectionItem = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    public interface ICompletionItem
    {
        VsComplectionItem CreateVsCompletionItem(IAsyncCompletionSource asyncCompletionSource);
        Task<object> GetDescriptionAsync(IIntelliSenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken);
    }

    internal class CompletionItem : ICompletionItem
    {
        private readonly IntelliSenseInfo _info;
        private readonly ImageElement _imageElement;

        public CompletionItem(IntelliSenseInfo info, ImageElement imageElement)
        {
            _info = info;
            _imageElement = imageElement;
        }

        public VsComplectionItem CreateVsCompletionItem(IAsyncCompletionSource asyncCompletionSource)
        {
            var completionItem = new VsComplectionItem(_info.Symbol, asyncCompletionSource, _imageElement);
            completionItem.Properties.AddProperty(typeof(ICompletionItem), this);
            return completionItem;
        }

        public Task<object> GetDescriptionAsync(IIntelliSenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            descriptionBuilder.GetDescriptionAsync(_info, cancellationToken);
    }

    public static class VsCompletionItemExtension
    {
        public static ICompletionItem GetRadCompletionItem(this VsComplectionItem vsComplection) => 
            vsComplection.Properties.GetProperty<ICompletionItem>(typeof(ICompletionItem));

        public static Task<object> GetDescriptionAsync(this VsComplectionItem vsComplection, IIntelliSenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            vsComplection.GetRadCompletionItem().GetDescriptionAsync(descriptionBuilder, cancellationToken);
    }
}
