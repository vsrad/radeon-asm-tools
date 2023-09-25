using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.IntelliSense.Navigation;
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
        private readonly NavigationToken _token;
        private readonly ImageElement _imageElement;

        public CompletionItem(NavigationToken navigationToken, ImageElement imageElement)
        {
            _token = navigationToken;
            _imageElement = imageElement;
        }

        public VsComplectionItem CreateVsCompletionItem(IAsyncCompletionSource asyncCompletionSource)
        {
            var completionItem = new VsComplectionItem(_token.GetText(), asyncCompletionSource, _imageElement);
            completionItem.Properties.AddProperty(typeof(ICompletionItem), this);
            return completionItem;
        }

        public Task<object> GetDescriptionAsync(IIntelliSenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            descriptionBuilder.GetColorizedDescriptionAsync(new[] { _token }, cancellationToken);
    }

    internal class MultipleCompletionItem : ICompletionItem
    {
        private readonly string _name;
        private readonly IReadOnlyList<NavigationToken> _tokens;
        private readonly ImageElement _imageElement;

        public MultipleCompletionItem(string name, IReadOnlyList<NavigationToken> tokens, ImageElement imageElement)
        {
            _name = name;
            _tokens = tokens;
            _imageElement = imageElement;
        }

        public VsComplectionItem CreateVsCompletionItem(IAsyncCompletionSource asyncCompletionSource)
        {
            var completionItem = new VsComplectionItem(_name, asyncCompletionSource, _imageElement);
            completionItem.Properties.AddProperty(typeof(ICompletionItem), this);
            return completionItem;
        }

        public Task<object> GetDescriptionAsync(IIntelliSenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            descriptionBuilder.GetColorizedDescriptionAsync(_tokens, cancellationToken);
    }

    public static class VsCompletionItemExtension
    {
        public static ICompletionItem GetRadCompletionItem(this VsComplectionItem vsComplection) => 
            vsComplection.Properties.GetProperty<ICompletionItem>(typeof(ICompletionItem));

        public static Task<object> GetDescriptionAsync(this VsComplectionItem vsComplection, IIntelliSenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            vsComplection.GetRadCompletionItem().GetDescriptionAsync(descriptionBuilder, cancellationToken);
    }
}
