using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    internal class InstructionSignature : SyntaxSignature
    {
        private readonly IInstructionToken _token;
        private readonly int _initParameterIdx;

        public InstructionSignature(ITrackingSpan span, IInstructionToken token, int parameterIdx = 0)
        {
            ApplicableToSpan = span;
            _initParameterIdx = parameterIdx;
            _token = token;
        }

        protected override void Initialize()
        {
            if (_content != null) return;

            var displayParts = new List<TextTag>();
            var parameterTokens = _token.Parameters.ToList();
            var content = new StringBuilder(_token.GetText());
            var parameters = new List<IParameter>();
            var start = content.Length;

            displayParts.AddTag(_token.Type, 0, content.Length);
            displayParts.AddTag(RadAsmTokenType.Structural, content.Length, 1);
            content.Append(" ");

            for (var i = 0; i < parameterTokens.Count; i++)
            {
                var token = parameterTokens[i];
                var tokenName = token.GetText();

                if (i > 0)
                {
                    displayParts.AddTag(RadAsmTokenType.Structural, content.Length, 2);
                    content.Append(", ");
                    start = content.Length;
                }

                content.Append(tokenName);

                var paramSpan = new Span(start, content.Length - start);
                displayParts.AddTag(token.Type, paramSpan);

                parameters.Add(new Parameter(
                    this,
                    tokenName,
                    string.Empty,
                    paramSpan,
                    paramSpan));

            }

            _content = content.ToString();
            _displayParts = new ReadOnlyCollection<TextTag>(displayParts);
            _parameters = new ReadOnlyCollection<IParameter>(parameters);
            _documentation = string.Empty;

            SetCurrentParameter(_initParameterIdx);
        }
    }
}