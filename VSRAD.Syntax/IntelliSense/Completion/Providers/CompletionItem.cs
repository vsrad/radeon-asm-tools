using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
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
            var completionItem = new VsComplectionItem(_token.AnalysisToken.Text, asyncCompletionSource, _imageElement);
            completionItem.Properties.AddProperty(typeof(ICompletionItem), this);
            return completionItem;
        }

        public Task<object> GetDescriptionAsync(IIntellisenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            descriptionBuilder.GetColorizedDescriptionAsync(_token, cancellationToken);
    }

    internal class InstructionCompletionItem : ICompletionItem
    {
        private readonly ImageElement _imageElement;
        private readonly Instruction _instruction;

        public InstructionCompletionItem(Instruction instruction, ImageElement imageElement)
        {
            _instruction = instruction;
            _imageElement = imageElement;
        }

        public VsComplectionItem CreateVsCompletionItem(IAsyncCompletionSource asyncCompletionSource)
        {
            var completionItem = new VsComplectionItem(_instruction.Text, asyncCompletionSource, _imageElement);
            completionItem.Properties.AddProperty(typeof(ICompletionItem), this);
            return completionItem;
        }

        public Task<object> GetDescriptionAsync(IIntellisenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            descriptionBuilder.GetColorizedDescriptionAsync(_instruction.Navigations, cancellationToken);
    }

    public static class VsCompletionItemExtension
    {
        public static ICompletionItem GetRadCompletionItem(this VsComplectionItem vsCompletion) => 
            vsCompletion.Properties.GetProperty<ICompletionItem>(typeof(ICompletionItem));

        public static Task<object> GetDescriptionAsync(this VsComplectionItem vsCompletion, IIntellisenseDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken) =>
            vsCompletion.GetRadCompletionItem().GetDescriptionAsync(descriptionBuilder, cancellationToken);
    }
}
