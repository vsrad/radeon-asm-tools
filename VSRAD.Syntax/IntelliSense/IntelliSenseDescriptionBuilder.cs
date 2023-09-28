using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Options;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.Text.Tagging
{
    public interface ITaggerMetadata : IContentTypeMetadata
    {
        IEnumerable<Type> TagTypes { get; }
    }

    public interface INamedTaggerMetadata : ITaggerMetadata, INamedContentTypeMetadata
    {
    }
}

namespace VSRAD.Syntax.IntelliSense
{
    public interface IIntelliSenseDescriptionBuilder
    {
        Task<object> GetDescriptionAsync(IntelliSenseInfo info, CancellationToken cancellationToken);
    }

    [Export(typeof(IIntelliSenseDescriptionBuilder))]
    internal class IntelliSenseDescriptionBuilder : IIntelliSenseDescriptionBuilder
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly ContentTypeManager _contentTypeManager;
        private readonly IClassifierProvider _asmSyntaxClassifierProvider;
        private readonly ITaggerProvider _asmSyntaxClassificationTaggerProvider;

        private readonly Dictionary<IContentType, IDocument> _tempDocuments
            = new Dictionary<IContentType, IDocument>();

        [ImportingConstructor]
        public IntelliSenseDescriptionBuilder(
            IDocumentFactory documentFactory,
            ContentTypeManager contentTypeManager,
            [ImportMany] IEnumerable<Lazy<IClassifierProvider, INamedContentTypeMetadata>> classifierProviders,
            [ImportMany] IEnumerable<Lazy<ITaggerProvider, INamedTaggerMetadata>> taggerProviders)
        {
            _documentFactory = documentFactory;
            _contentTypeManager = contentTypeManager;
            _asmSyntaxClassifierProvider = classifierProviders
                .First(p => p.Metadata.ContentTypes.Contains(Constants.RadeonAsmSyntaxContentType)).Value;
            _asmSyntaxClassificationTaggerProvider = taggerProviders
                .First(p => p.Metadata.ContentTypes.Contains(Constants.RadeonAsmSyntaxContentType) && p.Metadata.TagTypes.Contains(typeof(ClassificationTag))).Value;
        }

        public async Task<object> GetDescriptionAsync(IntelliSenseInfo info, CancellationToken cancellationToken)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            var descriptionBuilder = new ClassifiedTextBuilder();
            if (info.Definitions.Count != 0)
            {
                await AppendTokenDefinitionDescriptionAsync(descriptionBuilder, info.Definitions[0], cancellationToken);
                AppendTokenDefinitionList(descriptionBuilder, info.Definitions, cancellationToken);
            }
            else if (info.BuiltinInfo is BuiltinInfo builtinInfo)
            {
                await AppendTokenBuiltinInfoAsync(descriptionBuilder, info.AsmType, builtinInfo);
            }
            return descriptionBuilder.Build();
        }

        private async Task AppendTokenDefinitionDescriptionAsync(ClassifiedTextBuilder builder, NavigationToken definition, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var typeName = definition.Type.GetName();
            var document = _documentFactory.GetOrCreateDocument(definition.AnalysisToken.Snapshot.TextBuffer);

            if (definition.Type == RadAsmTokenType.Instruction)
            {
                builder
                    .AddClassifiedText($"({typeName} ")
                    .AddClassifiedText(RadAsmTokenType.Instruction, Path.GetFileNameWithoutExtension(definition.Path))
                    .AddClassifiedText(") ");
            }
            else
            {
                builder.AddClassifiedText($"({typeName}) ");
            }

            builder.AddClassifiedText(definition);

            if (definition.Type == RadAsmTokenType.FunctionName)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var documentAnalysis = await document.DocumentAnalysis.GetAnalysisResultAsync(definition.AnalysisToken.Snapshot);
                var block = documentAnalysis.GetBlock(definition.GetEnd());

                cancellationToken.ThrowIfCancellationRequested();

                if (block is FunctionBlock functionBlock)
                {
                    for (var i = 0; i < functionBlock.Parameters.Count; i++)
                    {
                        builder.AddClassifiedText(" ")
                            .AddClassifiedText(new NavigationToken(document, functionBlock.Parameters[i]));
                        if (i != functionBlock.Parameters.Count - 1)
                            builder.AddClassifiedText(",");
                    }
                }
            }

            builder.SetAsElement();

            if (TryGetCommentDescription(document.DocumentTokenizer, definition.GetEnd(), cancellationToken, out var message))
                builder.AddClassifiedText(message).SetAsElement();
        }

        private void AppendTokenDefinitionList(ClassifiedTextBuilder builder, IReadOnlyCollection<NavigationToken> tokens, CancellationToken cancellationToken)
        {
            builder.AddClassifiedText("").SetAsElement();

            var showFilePath = tokens.Count > 1;
            var showTypeName = tokens.Distinct().Count() > 1;
            foreach (var tokenGroup in tokens.GroupBy(t => t.Path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (showFilePath)
                {
                    var filePath = tokenGroup.All(t => t.Type == RadAsmTokenType.Instruction)
                        ? Path.GetFileNameWithoutExtension(tokenGroup.Key)
                        : tokenGroup.Key;
                    builder.AddClassifiedText(filePath, ClassifiedTextRunStyle.Bold).SetAsElement();
                }
                foreach (var token in tokenGroup)
                {
                    if (token.Type != RadAsmTokenType.Instruction)
                    {
                        builder.AddClassifiedText($"Defined on line {token.Line + 1}:", ClassifiedTextRunStyle.Bold).SetAsElement();
                    }
                    if (showTypeName)
                    {
                        var typeName = token.Type.GetName();
                        builder.AddClassifiedText($"({typeName}) ");
                    }
                    var definitionLine = token.SnapshotLine.Extent;
                    foreach (var (span, classification) in GetColorizedSpans(definitionLine))
                        builder.AddClassifiedText(span, classification);
                    builder.SetAsElement();
                }
            }
        }

        private async Task AppendTokenBuiltinInfoAsync(ClassifiedTextBuilder builder, AsmType asmType, BuiltinInfo builtinInfo)
        {
            var typeName = RadAsmTokenType.BuiltinFunction.GetName();
            builder
                .AddClassifiedText($"({typeName}) ")
                .AddClassifiedText(RadAsmTokenType.BuiltinFunction, builtinInfo.Name)
                .AddClassifiedText("(" + string.Join(", ", builtinInfo.Parameters) + ")")
                .SetAsElement();
            builder
                .AddClassifiedText(builtinInfo.Description)
                .SetAsElement();

            if (!string.IsNullOrEmpty(builtinInfo.Examples))
            {
                builder
                    .AddClassifiedText("").SetAsElement()
                    .AddClassifiedText("Examples:", ClassifiedTextRunStyle.Bold).SetAsElement();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var exampleDocument = GetTempDocument(_contentTypeManager.DetermineContentType(asmType));
                exampleDocument.CurrentSnapshot.TextBuffer.Replace(new Span(0, exampleDocument.CurrentSnapshot.Length), builtinInfo.Examples);
                var exampleSpan = new SnapshotSpan(exampleDocument.CurrentSnapshot, 0, exampleDocument.CurrentSnapshot.Length);
                await exampleDocument.DocumentAnalysis.GetAnalysisResultAsync(exampleDocument.CurrentSnapshot);
                foreach (var (span, classification) in GetColorizedSpans(exampleSpan))
                    builder.AddClassifiedText(span, classification);
                builder.SetAsElement();
            }
        }

        private List<(SnapshotSpan Span, string ClassificationTypeName)> GetColorizedSpans(SnapshotSpan snapshotSpan)
        {
            var colorizedSpans = new List<(SnapshotSpan Span, string ClassificationTypeName)>();
            var defnitionTagger = _asmSyntaxClassificationTaggerProvider.CreateTagger<ClassificationTag>(snapshotSpan.Snapshot.TextBuffer);
            var definitionTags = defnitionTagger.GetTags(new NormalizedSnapshotSpanCollection(snapshotSpan));
            foreach (var t in definitionTags)
                colorizedSpans.Add((t.Span, t.Tag.ClassificationType.Classification));

            var definitionClassifier = _asmSyntaxClassifierProvider.GetClassifier(snapshotSpan.Snapshot.TextBuffer);
            var definitionClassifications = definitionClassifier.GetClassificationSpans(snapshotSpan);
            foreach (var c in definitionClassifications)
                colorizedSpans.Add((c.Span, c.ClassificationType.Classification));

            colorizedSpans.Sort((a, b) => a.Span.Start.CompareTo(b.Span.Start));
            for (var i = 0; i <= colorizedSpans.Count; ++i)
            {
                int prevSpanEnd = i == 0 ? snapshotSpan.Start.Position : colorizedSpans[i - 1].Span.End.Position;
                int nextSpanStart = i == colorizedSpans.Count ? snapshotSpan.End.Position : colorizedSpans[i].Span.Start.Position;
                if (prevSpanEnd < nextSpanStart)
                {
                    var unclassifiedSpan = (new SnapshotSpan(snapshotSpan.Snapshot, prevSpanEnd, nextSpanStart - prevSpanEnd), RadAsmTokenType.Unknown.GetClassificationTypeName());
                    colorizedSpans.Insert(i, unclassifiedSpan);
                }
            }

            return colorizedSpans;
        }

        private IDocument GetTempDocument(IContentType contentType)
        {
            if (!_tempDocuments.TryGetValue(contentType, out var tempDocument))
            {
                // GetTempFileName creates a new file, which we can delete after opening a VS document
                var tempDocumentPath = Path.GetTempFileName();
                tempDocument = _documentFactory.GetOrCreateDocument(tempDocumentPath, contentType);
                _tempDocuments.Add(contentType, tempDocument);
                File.Delete(tempDocumentPath);
            }
            return tempDocument;
        }

        private bool TryGetCommentDescription(IDocumentTokenizer documentTokenizer, SnapshotPoint tokenEnd, CancellationToken cancellationToken, out string message)
        {
            var snapshot = tokenEnd.Snapshot;
            var currentLine = tokenEnd.GetContainingLine();
            var tokenizerResult = documentTokenizer.CurrentResult;

            var tokenLineComment = tokenizerResult
                .GetTokens(new Span(tokenEnd, currentLine.End - tokenEnd))
                .Where(t => documentTokenizer.GetTokenType(t.Type) == RadAsmTokenType.Comment);

            if (tokenLineComment.Any())
            {
                var text = tokenLineComment.First().GetText(snapshot);
                message = GetCommentText(text);
                return true;
            }

            var lines = new LinkedList<string>();
            var currentLineNumber = currentLine.LineNumber - 1;
            while (currentLineNumber >= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                currentLine = snapshot.GetLineFromLineNumber(currentLineNumber);
                var tokensAtLine = tokenizerResult
                    .GetTokens(new Span(currentLine.Start, currentLine.Length))
                    .Where(t => documentTokenizer.GetTokenType(t.Type) != RadAsmTokenType.Whitespace);

                if (tokensAtLine.Count() == 1)
                {
                    var trackingToken = tokensAtLine.First();
                    if (documentTokenizer.GetTokenType(trackingToken.Type) == RadAsmTokenType.Comment)
                    {
                        var tokenSpan = new SnapshotSpan(snapshot, trackingToken.GetSpan(snapshot));

                        lines.AddFirst(tokenSpan.GetText());
                        currentLineNumber = tokenSpan.Start.GetContainingLine().LineNumber - 1;
                        continue;
                    }
                }

                break;
            }

            if (lines.Count != 0)
            {
                message = GetCommentText(string.Join(System.Environment.NewLine, lines));
                return true;
            }

            message = null;
            return false;
        }

        private static string GetCommentText(string text)
        {
            var comment = text.Trim('/', '*', ' ', '\t', '\r', '\n', '\f');
            return Regex.Replace(comment, @"(?<=\n)\s*(\*|\/\/)\s?", "", RegexOptions.Compiled);
        }

        public class ClassifiedTextBuilder
        {
            private readonly LinkedList<ClassifiedTextRun> _classifiedTextRuns;
            private readonly LinkedList<ClassifiedTextElement> _classifiedTextElements;

            public ClassifiedTextBuilder()
            {
                _classifiedTextRuns = new LinkedList<ClassifiedTextRun>();
                _classifiedTextElements = new LinkedList<ClassifiedTextElement>();
            }

            public ContainerElement Build() =>
                new ContainerElement(ContainerElementStyle.Stacked, _classifiedTextElements);

            public ClassifiedTextBuilder SetAsElement()
            {
                _classifiedTextElements.AddLast(new ClassifiedTextElement(_classifiedTextRuns));
                _classifiedTextRuns.Clear();
                return this;
            }

            public ClassifiedTextBuilder AddClassifiedText(NavigationToken navigationToken)
            {
                _classifiedTextRuns.AddLast(new ClassifiedTextRun(navigationToken.Type.GetClassificationTypeName(), navigationToken.GetText(), navigationToken.Navigate));
                return this;
            }

            public ClassifiedTextBuilder AddClassifiedText(SnapshotSpan span, string classificationType)
            {
                _classifiedTextRuns.AddLast(new ClassifiedTextRun(classificationType, span.GetText()));
                return this;
            }

            public ClassifiedTextBuilder AddClassifiedText(RadAsmTokenType tokenType, string text, ClassifiedTextRunStyle style = ClassifiedTextRunStyle.UseClassificationStyle)
            {
                _classifiedTextRuns.AddLast(new ClassifiedTextRun(tokenType.GetClassificationTypeName(), text, style));
                return this;
            }

            public ClassifiedTextBuilder AddClassifiedText(string text, ClassifiedTextRunStyle style = ClassifiedTextRunStyle.Plain) =>
                AddClassifiedText(RadAsmTokenType.Identifier, text, style);
        }
    }
}
