using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense
{
    public interface IIntellisenseDescriptionBuilder
    {
        object GetColorizedDescription(IReadOnlyList<NavigationToken> tokens);
        object GetColorizedDescription(NavigationToken token);
    }

    [Export(typeof(IIntellisenseDescriptionBuilder))]
    internal class IntellisenseDescriptionBuilder : IIntellisenseDescriptionBuilder
    {
        public object GetColorizedDescription(IReadOnlyList<NavigationToken> tokens)
        {
            if (tokens == null || tokens.Count == 0) return null;
            else if (tokens.Count == 1) return GetColorizedDescription(tokens[0]);
            else return GetColorizedDescriptions(tokens);
        }

        private object GetColorizedDescriptions(IReadOnlyList<NavigationToken> tokens)
        {
            var builder = new ClassifiedTextBuilder();
            foreach (var tokenGroup in tokens.GroupBy(t => t.Path))
            {
                var filePath = tokenGroup.All(t => t.Type == RadAsmTokenType.Instruction)
                    ? Path.GetFileNameWithoutExtension(tokenGroup.Key)
                    : tokenGroup.Key;

                builder.AddClassifiedText(filePath).SetAsElement();
                foreach (var token in tokenGroup)
                {
                    var typeName = token.Type.GetName();
                    var textBeforeToken = token.LineText.Substring(0, token.LineTokenStart);
                    var textAfterToken = token.LineText.Substring(token.LineTokenEnd);

                    builder.AddClassifiedText($"({typeName}) ")
                        .AddClassifiedText(textBeforeToken)
                        .AddClassifiedText(token)
                        .AddClassifiedText(textAfterToken)
                        .SetAsElement();
                }
            }

            return builder.Build();
        }

        public object GetColorizedDescription(NavigationToken token)
        {
            if (token == NavigationToken.Empty) return null;

            var typeName = token.Type.GetName();
            var builder = new ClassifiedTextBuilder();
            builder.AddClassifiedText($"({typeName}) ");
            if (token.Type == RadAsmTokenType.FunctionName)
            {

            }
            else if (token.Type == RadAsmTokenType.GlobalVariable || token.Type == RadAsmTokenType.LocalVariable)
            {
                var variableToken = (VariableToken)token.AnalysisToken;
                var defaultValue = variableToken.DefaultValue.GetText(variableToken.Snapshot);
                builder.AddClassifiedText(token)
                    .AddClassifiedText(" = ")
                    .AddClassifiedText(RadAsmTokenType.Number, defaultValue);
            }
            else
            {
                builder.AddClassifiedText(token);
            }

            builder.SetAsElement();
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

            public ClassifiedTextBuilder AddClassifiedText(NavigationToken navigationToken)
            {
                _classifiedTextRuns.AddLast(new ClassifiedTextRun(navigationToken.Type.GetClassificationTypeName(), navigationToken.GetText(), navigationToken.Navigate));
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
