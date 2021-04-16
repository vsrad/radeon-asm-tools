using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    internal class FunctionSignature : ISyntaxSignature
    {
        private readonly IFunctionBlock _block;
        private readonly SignatureConfig _signatureConfig;
        private string _content;
        private IParameter _currentParameter;
        private ReadOnlyCollection<IParameter> _parameters;
        private ReadOnlyCollection<TextTag> _displayParts;
        private int _parameterIdx;

        public FunctionSignature(ITrackingSpan span, IFunctionBlock block, SignatureConfig signatureConfig, int parameterIdx = 0)
        {
            ApplicableToSpan = span;
            _parameterIdx = parameterIdx;
            _block = block;
            _signatureConfig = signatureConfig;
        }

        public void Initialize()
        {
            if (_content != null) return;

            var displayParts = new List<TextTag>();
            var parameterTokens = _block.Parameters;
            var content = new StringBuilder(_block.Name.GetText());
            var parameters = new List<IParameter>();
            var start = content.Length;

            displayParts.AddTag(_block.Name.Type, 0, content.Length);
            displayParts.AddTag(RadAsmTokenType.LcurveBracket, content.Length, _signatureConfig.SignatureStart.Length);
            content.Append(_signatureConfig.SignatureStart);

            for (var i = 0; i < parameterTokens.Count; i++)
            {
                var token = parameterTokens[i];
                var tokenName = token.GetText();

                if (i > 0)
                {
                    displayParts.AddTag(RadAsmTokenType.Comma, content.Length, 2);
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

            displayParts.AddTag(RadAsmTokenType.RcurveBracket, content.Length, _signatureConfig.SignatureEnd.Length);
            content.Append(_signatureConfig.SignatureEnd);

            _content = content.ToString();
            _displayParts = new ReadOnlyCollection<TextTag>(displayParts);
            _parameters = new ReadOnlyCollection<IParameter>(parameters);

            SetCurrentParameter(_parameterIdx);
        }

        public void SetCurrentParameter(int idx)
        {
            if (_parameters.Count <= idx) return;

            var newValue = _parameters[idx];
            if (newValue == _currentParameter) return;

            var old = _currentParameter;
            _currentParameter = newValue;
            _parameterIdx = idx;
            CurrentParameterChanged?.Invoke(this, new CurrentParameterChangedEventArgs(old, newValue));
        }

        public ITrackingSpan ApplicableToSpan { get; }

        public string Documentation => _block.Name.GetDescription() ?? string.Empty;

        public string Content
        {
            get
            {
                Initialize();
                return _content;
            }
        }

        public string PrettyPrintedContent
        {
            get
            {
                Initialize();
                return _content;
            }
        }

        public ReadOnlyCollection<IParameter> Parameters
        {
            get
            {
                Initialize();
                return _parameters;
            }
        }

        public IParameter CurrentParameter
        {
            get
            {
                Initialize();
                return _currentParameter;
            }
        }

        public ReadOnlyCollection<TextTag> DisplayParts
        {
            get
            {
                Initialize();
                return _displayParts;
            }
        }

        public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;
    }
}