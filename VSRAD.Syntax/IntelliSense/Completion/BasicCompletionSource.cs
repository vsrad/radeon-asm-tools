using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal abstract class BasicCompletionSource : IAsyncCompletionSource
    {
        public readonly IParserManager ParserManager;

        public BasicCompletionSource(OptionsProvider optionsProvider, IParserManager parserManager)
        {
            ParserManager = parserManager;
            optionsProvider.OptionsUpdated += DisplayOptionsUpdated;
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            if (ParserManager.ActualParser == null
                || trigger.Reason == CompletionTriggerReason.Backspace
                || trigger.Reason == CompletionTriggerReason.Deletion
                || trigger.Character == '\n'
                || trigger.Character == ' '
                || ParserManager.ActualParser.PointInComment(triggerLocation))
                return CompletionStartData.DoesNotParticipateInCompletion;

            var extent = triggerLocation.GetExtent();
            if (extent.IsSignificant && extent.Span.Length > 2)
                return new CompletionStartData(CompletionParticipation.ProvidesItems, extent.Span);

            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        public static ImageElement GetImageElement(int imageId) =>
            new ImageElement(new ImageId(ImageCatalogGuid, imageId));

        public abstract Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token);

        public abstract Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token);

        protected abstract void DisplayOptionsUpdated(OptionsProvider options);

        private static readonly Guid ImageCatalogGuid = Guid.Parse(/* image catalog guid */ "ae27a6b0-e345-4288-96df-5eaf394ee369");
    }
}
