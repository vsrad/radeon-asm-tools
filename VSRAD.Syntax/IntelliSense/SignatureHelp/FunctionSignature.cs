using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    internal class FunctionSignature : SyntaxSignature
    {
        private readonly IFunctionBlock _block;
        private readonly SignatureConfig _signatureConfig;
        private readonly int _initParameterIdx;

        public FunctionSignature(ITrackingSpan span, IFunctionBlock block, SignatureConfig signatureConfig, int parameterIdx = 0)
        {
            ApplicableToSpan = span;
            _block = block;
            _signatureConfig = signatureConfig;
            _initParameterIdx = parameterIdx;
        }

        protected override void Initialize()
        {
            if (_content != null) return;

            var displayParts = new List<TextTag>();
            var parameterTokens = _block.Parameters;
            var content = new StringBuilder(_block.Name.GetText());
            var parameters = new List<IParameter>();
            var start = content.Length;

            displayParts.AddTag(_block.Name.Type, 0, content.Length);
            displayParts.AddTag(RadAsmTokenType.Structural, content.Length, _signatureConfig.SignatureStart.Length);
            content.Append(_signatureConfig.SignatureStart);

            for (var i = 0; i < parameterTokens.Count; i++)
            {
                var token = parameterTokens[i];
                var tokenName = token.GetText();

                if (i > 0)
                {
                    displayParts.AddTag(RadAsmTokenType.Structural, content.Length, 2);
                    content.Append(_signatureConfig.TriggerParameterChar).Append(" ");
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

            displayParts.AddTag(RadAsmTokenType.Structural, content.Length, _signatureConfig.SignatureEnd.Length);
            content.Append(_signatureConfig.SignatureEnd);

            _content = content.ToString();
            _displayParts = new ReadOnlyCollection<TextTag>(displayParts);
            _parameters = new ReadOnlyCollection<IParameter>(parameters);
            _documentation = _block.Name.GetDescription() ?? string.Empty;

            SetCurrentParameter(_initParameterIdx);
        }
    }
}