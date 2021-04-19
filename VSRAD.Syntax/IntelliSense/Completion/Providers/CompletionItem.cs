using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Options.Instructions;
using VsComplectionItem = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    public interface ICompletionItem
    {
        VsComplectionItem CreateVsCompletionItem(IAsyncCompletionSource asyncCompletionSource);
        Task<object> GetDescriptionAsync(IIntellisenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken);
    }

    internal class CompletionItem : ICompletionItem
    {
        private readonly INavigationToken _token;
        private readonly ImageElement _imageElement;

        public CompletionItem(INavigationToken navigationToken, ImageElement imageElement)
        {
            _token = navigationToken;
            _imageElement = imageElement;
        }

        public VsComplectionItem CreateVsCompletionItem(IAsyncCompletionSource asyncCompletionSource)
        {
            var completionItem = new VsComplectionItem(_token.Definition.GetText(), asyncCompletionSource, _imageElement);
            completionItem.Properties.AddProperty(typeof(ICompletionItem), this);
            return completionItem;
        }

        public Task<object> GetDescriptionAsync(IIntellisenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            descriptionBuilder.GetColorizedDescriptionAsync(_token, cancellationToken);
    }

    internal class InstructionCompletionItem : ICompletionItem
    {
        private readonly ImageElement _imageElement;
        private readonly IReadOnlyList<INavigationToken> _instructionNavigations;
        private readonly string _text;

        public InstructionCompletionItem(IEnumerable<Instruction> instructions, string text, ImageElement imageElement)
        {
            _instructionNavigations = instructions.SelectMany(i => i.Navigations).ToList();
            _text = text;
            _imageElement = imageElement;
        }

        public VsComplectionItem CreateVsCompletionItem(IAsyncCompletionSource asyncCompletionSource)
        {
            var completionItem = new VsComplectionItem(_text, asyncCompletionSource, _imageElement);
            completionItem.Properties.AddProperty(typeof(ICompletionItem), this);
            return completionItem;
        }

        public Task<object> GetDescriptionAsync(IIntellisenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            descriptionBuilder.GetColorizedDescriptionAsync(_instructionNavigations, cancellationToken);
    }

    public static class VsCompletionItemExtension
    {
        public static ICompletionItem GetRadCompletionItem(this VsComplectionItem vsCompletion) => 
            vsCompletion.Properties.GetProperty<ICompletionItem>(typeof(ICompletionItem));

        public static Task<object> GetDescriptionAsync(this VsComplectionItem vsCompletion, IIntellisenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            vsCompletion.GetRadCompletionItem().GetDescriptionAsync(descriptionBuilder, cancellationToken);
    }
}
