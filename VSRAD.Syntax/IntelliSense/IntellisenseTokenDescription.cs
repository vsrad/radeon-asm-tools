using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.IntelliSense.Navigation.NavigationList;
using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense
{
    public interface IIntellisenseDescriptionBuilder
    {
        object GetColorizedDescription(IEnumerable<NavigationToken> tokens);
    }

    [Export(typeof(IIntellisenseDescriptionBuilder))]
    internal class IntellisenseDescriptionBuilder : IIntellisenseDescriptionBuilder
    {
        private readonly Lazy<INavigationTokenService> _navigationTokenService;

        [ImportingConstructor]
        IntellisenseDescriptionBuilder(Lazy<INavigationTokenService> navigationTokenService)
        {
            _navigationTokenService = navigationTokenService;
        }

        public object GetColorizedDescription(IEnumerable<NavigationToken> tokens)
        {
            try
            {
                return tokens.Count() == 1
                    ? GetColorizedTokenDescription(tokens.First())
                    : GetColorizedTokensDescription(tokens);
            }
            catch (Exception e)
            {
                Error.LogError(e, "Token colorized description");
                return null;
            }
        }

        private object GetColorizedTokensDescription(IEnumerable<NavigationToken> tokens)
        {
            var elements = new List<object>();
            foreach (var group in tokens.Select(t => new DefinitionToken(t)).GroupBy(d => d.FilePath))
            {
                // for instructions show only the name of the file with documentation without .radasm1 and .radasm2 extensions
                var filePath = group.All(t => t.NavigationToken.AnalysisToken.Type == RadAsmTokenType.Instruction)
                        ? Path.GetFileNameWithoutExtension(group.Key)
                        : group.Key;

                elements.Add(new ClassifiedTextElement(new ClassifiedTextRun(RadAsmTokenType.Identifier.GetClassificationTypeName(), filePath)));

                foreach (var token in group)
                {
                    var navigationToken = token.NavigationToken;
                    var analysisToken = navigationToken.AnalysisToken;
                    var typeName = analysisToken.Type.GetName();

                    var spanBeforeToken = new SnapshotSpan(token.Line.Start, navigationToken.GetStart());
                    var spanAfterToken = new SnapshotSpan(navigationToken.GetEnd(), token.Line.End);
                    elements.Add(new ClassifiedTextElement(new ClassifiedTextRun[]
                    {
                        new ClassifiedTextRun(RadAsmTokenType.Identifier.GetClassificationTypeName(), $"({typeName}) "),
                        new ClassifiedTextRun(RadAsmTokenType.Structural.GetClassificationTypeName(), $"{spanBeforeToken.GetText()}"),
                        GetNameElement(navigationToken),
                        new ClassifiedTextRun(RadAsmTokenType.Structural.GetClassificationTypeName(), $"{spanAfterToken.GetText()}"),
                    }));
                }

                elements.Add(new ClassifiedTextElement(new ClassifiedTextRun(RadAsmTokenType.Whitespace.GetClassificationTypeName(), "")));
            }

            return new ContainerElement(ContainerElementStyle.Stacked, elements);
        }

        private object GetColorizedTokenDescription(NavigationToken token)
        {
            var version = token.Snapshot;
            if (!version.TryGetDocumentAnalysis(out var documentAnalysis))
                return null;

            var description = GetTokenDescription(token, documentAnalysis);
            var type = token.AnalysisToken.Type;
            var typeName = type.GetName();

            ClassifiedTextElement textElement = null;
            if (type == RadAsmTokenType.FunctionName)
            {
                var fb = GetFunctionBlockByToken(documentAnalysis, token.AnalysisToken);
                if (fb == null)
                    return null;

                var addBrackets = version.GetAsmType() == AsmType.RadAsm2;
                var textRuns = new List<ClassifiedTextRun>()
                {
                    GetNameElement(token),
                    new ClassifiedTextRun(RadAsmTokenType.Structural.GetClassificationTypeName(), addBrackets ? "(" : " "),
                };

                var arguments = fb.Tokens
                    .Where(t => t.Type == RadAsmTokenType.FunctionParameter)
                    .Select(t => new NavigationToken(t, version))
                    .ToArray();
                for (int i = 0; i < arguments.Length; i++)
                {
                    textRuns.Add(GetNameElement(arguments[i]));
                    if (i < arguments.Length - 1)
                        textRuns.Add(new ClassifiedTextRun(RadAsmTokenType.Structural.GetClassificationTypeName(), ", "));
                }

                if (addBrackets)
                    textRuns.Add(new ClassifiedTextRun(RadAsmTokenType.Structural.GetClassificationTypeName(), ")"));

                textElement = new ClassifiedTextElement(textRuns);
            }
            else if (type == RadAsmTokenType.GlobalVariable || type == RadAsmTokenType.LocalVariable)
            {
                var variable = (VariableToken)token.AnalysisToken;
                var textRuns = new List<ClassifiedTextRun>() { GetNameElement(token) };

                if (variable.DefaultValue != TrackingToken.Empty)
                {
                    textRuns.Add(new ClassifiedTextRun(RadAsmTokenType.Structural.GetClassificationTypeName(), " = "));
                    textRuns.Add(new ClassifiedTextRun(RadAsmTokenType.Number.GetClassificationTypeName(), variable.DefaultValue.GetText(version)));
                }

                textElement = new ClassifiedTextElement(textRuns);
            }
            else
            {
                var nameElement = GetNameElement(token);
                textElement = new ClassifiedTextElement(nameElement);
            }

            return GetDescriptionElement(typeName, textElement, description);
        }

        private ClassifiedTextRun GetNameElement(NavigationToken token)
        {
            string tokenText = token.GetText();
            Action navigate = () => _navigationTokenService.Value.GoToPoint(token);
            string classificationType = token.AnalysisToken.Type.GetDescriptionClassificationTypeName();

            return new ClassifiedTextRun(classificationType, tokenText, navigate);
        }

        private static string GetTokenDescription(NavigationToken token, DocumentAnalysis documentAnalysis)
        {
            string description = null;
            var type = token.AnalysisToken.Type;
            if (type == RadAsmTokenType.FunctionName
                || type == RadAsmTokenType.GlobalVariable
                || type == RadAsmTokenType.LocalVariable
                || type == RadAsmTokenType.Label)
            {
                var snapshot = token.Snapshot;
                var tokenSpan = token.AnalysisToken.TrackingToken.GetSpan(snapshot);
                var line = snapshot.GetLineFromPosition(tokenSpan.Start);

                // try get description to the right of the declaration
                var tokens = documentAnalysis.GetTokens(new Span(tokenSpan.End, line.EndIncludingLineBreak - tokenSpan.End));
                if (!GetDescriptionFromComment(documentAnalysis, snapshot, tokens, out description))
                {
                    // try get description above the declaration
                    line = snapshot.GetLineFromLineNumber(line.LineNumber - 1);
                    tokens = documentAnalysis.GetTokens(new Span(line.Start, line.EndIncludingLineBreak - line.Start));

                    GetDescriptionFromComment(documentAnalysis, snapshot, tokens, out description);
                }
            }
            else if (type == RadAsmTokenType.Instruction)
            {
                var definition = new DefinitionToken(token);
                description = definition.Line.GetText();
            }

            return description;
        }

        private static ContainerElement GetDescriptionElement(string typeName, ClassifiedTextElement nameElement, string description)
        {
            var tokenElement = new ContainerElement(
                ContainerElementStyle.Wrapped,
                new ClassifiedTextElement(
                    new ClassifiedTextRun(RadAsmTokenType.Identifier.GetClassificationTypeName(), $"({typeName}) ")
                ),
                nameElement
                );

            if (string.IsNullOrEmpty(description))
                return tokenElement;

            return new ContainerElement(
                    ContainerElementStyle.Stacked,
                    tokenElement,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(RadAsmTokenType.Structural.GetClassificationTypeName(), description)
                    )
                );
        }

        private static bool GetDescriptionFromComment(DocumentAnalysis documentAnalysis, ITextSnapshot version, IEnumerable<TrackingToken> tokens, out string description)
        {
            var commentTokens = tokens.Where(t => documentAnalysis.LexerTokenToRadAsmToken(t.Type) == RadAsmTokenType.Comment);

            if (commentTokens.Any())
            {
                description = commentTokens
                    .First()
                    .GetText(version)
                    .Trim(new char[] { '/', '*', ' ', '\r', '\n' })
                    .Replace("*", string.Empty);
                return true;
            }
            else
            {
                description = null;
                return false;
            }
        }

        private static FunctionBlock GetFunctionBlockByToken(DocumentAnalysis documentAnalysis, AnalysisToken functionToken)
        {
            foreach (var block in documentAnalysis.LastParserResult)
            {
                if (block.Type == BlockType.Function)
                {
                    var funcBlock = (FunctionBlock)block;
                    if (funcBlock.Name == functionToken)
                        return funcBlock;
                }
            }

            return null;
        }
    }
}
