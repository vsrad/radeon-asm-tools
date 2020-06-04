using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense.SignatureHelper
{
    internal class RadeonSignature : ISignature
    {
        private readonly FunctionBlock _block;
        private string _content, _ppContent;
        private IParameter _currentParameter;
        private ReadOnlyCollection<IParameter> _parameters;

        public RadeonSignature(ITrackingSpan span, FunctionBlock block)
        {
            ApplicableToSpan = span;
            _block = block;
        }

        public void Initialize()
        {
            if (_content != null)
                return;

            var parameterTokens = _block.GetArgumentTokens().ToArray();

            var content = new StringBuilder(_block.FunctionToken.TokenName);
            var ppContent = new StringBuilder(_block.FunctionToken.TokenName);
            var parameters = new IParameter[parameterTokens.Length];

            content.Append('(');
            ppContent.AppendLine("(");
            int start = content.Length, ppStart = ppContent.Length;
            for (int i = 0; i < parameterTokens.Length; i++)
            {
                ppContent.Append("    ");
                ppStart = ppContent.Length;

                if (i > 0)
                {
                    content.Append(", ");
                    start = content.Length;
                }

                content.Append(parameterTokens[i].TokenName);
                ppContent.Append(parameterTokens[i].TokenName);

                var paramSpan = new Span(start, content.Length - start);
                var ppParamSpan = new Span(ppStart, ppContent.Length - ppStart);

                ppContent.AppendLine(",");

                parameters[i] = new RadeonParameter(
                    this,
                    parameterTokens[i].TokenName,
                    doc: string.Empty,
                    paramSpan,
                    ppParamSpan
                );
            }
            content.Append(')');
            ppContent.Append(')');

            _content = content.ToString();
            _ppContent = ppContent.ToString();

            _parameters = new ReadOnlyCollection<IParameter>(parameters);
            if (parameterTokens.Length > 0)
                SetCurrentParameter(_parameters[0]);
        }

        private void SetCurrentParameter(IParameter newValue)
        {
            if (newValue != _currentParameter)
            {
                var old = _currentParameter;
                _currentParameter = newValue;
                CurrentParameterChanged?.Invoke(this, new CurrentParameterChangedEventArgs(old, newValue));
            }
        }

        public ITrackingSpan ApplicableToSpan { get; }

        public string Documentation => _block.FunctionToken.Description ?? string.Empty;

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
                return _ppContent;
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

        public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;
    }
}
