using System;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;
using VSRAD.Syntax.Core.Tokens;
// ReSharper disable InconsistentNaming

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    public class TextTag
    {
        public RadAsmTokenType Type { get; }
        public Span Span { get; }

        public TextTag(RadAsmTokenType type, Span span)
        {
            Type = type;
            Span = span;
        }
    }

    public interface ISyntaxSignature : ISignature
    {
        ReadOnlyCollection<TextTag> DisplayParts { get; }
        void SetCurrentParameter(int idx);
    }

    internal abstract class SyntaxSignature : ISyntaxSignature
    {
        protected string _content;
        protected string _documentation;
        protected ReadOnlyCollection<IParameter> _parameters;
        protected ReadOnlyCollection<TextTag> _displayParts;
        private IParameter _parameter;

        public void SetCurrentParameter(int idx)
        {
            if (_parameters == null || _parameters.Count <= idx) return;

            var newValue = _parameters[idx];
            if (newValue == _parameter) return;

            var old = _parameter;
            _parameter = newValue;
            CurrentParameterChanged?.Invoke(this, new CurrentParameterChangedEventArgs(old, newValue));
        }

        protected abstract void Initialize();

        public ITrackingSpan ApplicableToSpan { get; protected set; }

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

        public string Documentation
        {
            get
            {
                Initialize();
                return _documentation;
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
                return _parameter;
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
