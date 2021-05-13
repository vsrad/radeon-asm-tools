using System;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Core.Tokens;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using System.Threading;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense
{
    public interface IIntellisenseDescriptionBuilder
    {
        Task<object> GetColorizedDescriptionAsync(IReadOnlyList<INavigationToken> tokens, CancellationToken cancellationToken);
        Task<object> GetColorizedDescriptionAsync(INavigationToken token, CancellationToken cancellationToken);
    }

    [Export(typeof(IIntellisenseDescriptionBuilder))]
    internal class IntellisenseDescriptionBuilder : IIntellisenseDescriptionBuilder
    {
        private readonly INavigationTokenService _navigationTokenService;
        private readonly Lazy<IDocumentFactory> _documentFactoryLazy;

        [ImportingConstructor]
        public IntellisenseDescriptionBuilder(INavigationTokenService navigationTokenService, Lazy<IDocumentFactory> documentFactory)
        {
            _navigationTokenService = navigationTokenService;
            _documentFactoryLazy = documentFactory;
        }

        public async Task<object> GetColorizedDescriptionAsync(IReadOnlyList<INavigationToken> tokens, CancellationToken cancellationToken)
        {
            if (tokens == null || tokens.Count == 0) return null;
            if (tokens.Count == 1) return await GetColorizedDescriptionAsync(tokens[0], cancellationToken);
            return GetColorizedDescriptions(tokens, cancellationToken);
        }

        private object GetColorizedDescriptions(IReadOnlyList<INavigationToken> tokens, CancellationToken cancellationToken)
        {
            var builder = new ClassifiedTextBuilder();
            foreach (var tokenGroup in tokens.GroupBy(t => t.Document.Path))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = tokenGroup.All(t => t.Type == RadAsmTokenType.Instruction)
                    ? Path.GetFileNameWithoutExtension(tokenGroup.Key)
                    : tokenGroup.Key;

                builder.AddClassifiedText(filePath).SetAsElement();
                foreach (var token in tokenGroup)
                {
                    var tokenLine = token.GetLine();
                    var tokenLineText = tokenLine.LineText;
                    var lineTokenStart = token.GetStart() - tokenLine.LineStart;
                    var lineTokenEnd = token.GetEnd() - tokenLine.LineStart;
                    var typeName = token.Type.GetName();
                    var textBeforeToken = tokenLineText.Substring(0, lineTokenStart);
                    var textAfterToken = tokenLineText.Substring(lineTokenEnd);

                    builder.AddClassifiedText($"({typeName}) ")
                        .AddClassifiedText(textBeforeToken)
                        .AddClassifiedText(token)
                        .AddClassifiedText(textAfterToken)
                        .SetAsElement();
                }
            }

            return builder.Build();
        }

        public async Task<object> GetColorizedDescriptionAsync(INavigationToken token, CancellationToken cancellationToken)
        {
            if (token == null) return null;
            cancellationToken.ThrowIfCancellationRequested();

            var typeName = token.Type.GetName();
            var document = token.Document;
            var snapshot = document.CurrentSnapshot;

            var builder = new ClassifiedTextBuilder();

            if (token.Type == RadAsmTokenType.Instruction)
            {
                builder
                    .AddClassifiedText($"({typeName} ")
                    .AddClassifiedText(RadAsmTokenType.Instruction, Path.GetFileNameWithoutExtension(document.Path))
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
                var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(snapshot);
                var block = analysisResult.GetBlock(token.GetEnd());

                cancellationToken.ThrowIfCancellationRequested();

                if (block is FunctionBlock functionBlock)
                {
                    for (var i = 0; i < functionBlock.Parameters.Count; i++)
                    {
                        builder.AddClassifiedText(" ")
                            .AddClassifiedText(_navigationTokenService.CreateToken(functionBlock.Parameters[i], document));
                        if (i != functionBlock.Parameters.Count - 1)
                            builder.AddClassifiedText(",");
                    }
                }
            }
            else if (token.Type == RadAsmTokenType.GlobalVariable || token.Type == RadAsmTokenType.LocalVariable)
            {
                var variableToken = (VariableToken)token.Definition;
                if (variableToken.DefaultValue != default)
                {
                    var defaultValueText = variableToken.DefaultValue.GetText(snapshot);
                    builder.AddClassifiedText(" = ")
                        .AddClassifiedText(RadAsmTokenType.Number, defaultValueText);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            builder.SetAsElement();
            if (token.Definition is IDefinitionToken definitionToken)
            {
                var description = definitionToken.GetDescription(_documentFactoryLazy.Value);
                if (description != null)
                    builder.AddClassifiedText(description).SetAsElement();
            }
            return builder.Build();
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

            public ClassifiedTextBuilder AddClassifiedText(INavigationToken navigationToken)
            {
                _classifiedTextRuns.AddLast(new ClassifiedTextRun(navigationToken.Type.GetClassificationTypeName(), navigationToken.Definition.GetText(), navigationToken.Navigate));
                return this;
            }

            public ClassifiedTextBuilder AddClassifiedText(RadAsmTokenType tokenType, string text)
            {
                _classifiedTextRuns.AddLast(new ClassifiedTextRun(tokenType.GetClassificationTypeName(), text));
                return this;
            }

            public ClassifiedTextBuilder AddClassifiedText(string text) =>
                AddClassifiedText(RadAsmTokenType.Identifier, text);
        }
    }
}
