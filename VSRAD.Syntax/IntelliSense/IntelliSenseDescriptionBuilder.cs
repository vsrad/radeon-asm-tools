using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
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
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense
{
    public interface IIntelliSenseDescriptionBuilder
    {
        Task<object> GetColorizedDescriptionAsync(IReadOnlyCollection<NavigationToken> tokens, CancellationToken cancellationToken);
    }

    [Export(typeof(IIntelliSenseDescriptionBuilder))]
    internal class IntelliSenseDescriptionBuilder : IIntelliSenseDescriptionBuilder
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly IIntelliSenseService _intelliSenseService;

        [ImportingConstructor]
        public IntelliSenseDescriptionBuilder(IDocumentFactory documentFactory, IIntelliSenseService intelliSenseService)
        {
            _documentFactory = documentFactory;
            _intelliSenseService = intelliSenseService;
        }

        public async Task<object> GetColorizedDescriptionAsync(IReadOnlyCollection<NavigationToken> tokens, CancellationToken cancellationToken)
        {
            if (tokens == null)
                throw new ArgumentNullException(nameof(tokens));
            if (tokens.Count == 0)
                throw new ArgumentException($"{nameof(tokens)} is empty");

            var descriptionBuider = await GetTokenDescriptionBuilderAsync(tokens.First(), cancellationToken);
            descriptionBuider = AppendTokenDefinitionsToDescription(descriptionBuider, tokens, cancellationToken);

            return descriptionBuider.Build();
        }

        private ClassifiedTextBuilder AppendTokenDefinitionsToDescription(ClassifiedTextBuilder builder, IReadOnlyCollection<NavigationToken> tokens, CancellationToken cancellationToken)
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
                    var textBeforeToken = token.LineText.Substring(0, token.LineTokenStart).TrimStart();
                    var textAfterToken = token.LineText.Substring(token.LineTokenEnd).TrimEnd();
                    builder
                        .AddClassifiedText(textBeforeToken)
                        .AddClassifiedText(token)
                        .AddClassifiedText(textAfterToken)
                        .SetAsElement();
                }
            }

            return builder;
        }

        private async Task<ClassifiedTextBuilder> GetTokenDescriptionBuilderAsync(NavigationToken token, CancellationToken cancellationToken)
        {
            if (token == NavigationToken.Empty) return null;
            cancellationToken.ThrowIfCancellationRequested();

            var typeName = token.Type.GetName();
            var document = _documentFactory.GetOrCreateDocument(token.AnalysisToken.Snapshot.TextBuffer);

            var builder = new ClassifiedTextBuilder();

            if (token.Type == RadAsmTokenType.Instruction)
            {
                builder
                    .AddClassifiedText($"({typeName} ")
                    .AddClassifiedText(RadAsmTokenType.Instruction, Path.GetFileNameWithoutExtension(token.Path))
                    .AddClassifiedText(") ");
            }
            else
            {
                builder.AddClassifiedText($"({typeName}) ");
            }

            builder.AddClassifiedText(token);
            if (token.Type == RadAsmTokenType.FunctionName)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var documentAnalysis = await document.DocumentAnalysis.GetAnalysisResultAsync(token.AnalysisToken.Snapshot);
                var block = documentAnalysis.GetBlock(token.GetEnd());

                cancellationToken.ThrowIfCancellationRequested();

                if (block is FunctionBlock functionBlock)
                {
                    for (var i = 0; i < functionBlock.Parameters.Count; i++)
                    {
                        builder.AddClassifiedText(" ")
                            .AddClassifiedText(_intelliSenseService.CreateToken(functionBlock.Parameters[i], document));
                        if (i != functionBlock.Parameters.Count - 1)
                            builder.AddClassifiedText(",");
                    }
                }
            }

            builder.SetAsElement();
            if (TryGetCommentDescription(document.DocumentTokenizer, token.GetEnd(), cancellationToken, out var message))
                builder.AddClassifiedText(message).SetAsElement();
            return builder;
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
