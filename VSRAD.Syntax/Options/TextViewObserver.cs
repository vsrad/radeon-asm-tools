using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(TextViewObserver))]
    internal sealed class TextViewObserver
    {
        private readonly List<IWpfTextView> _textViews;
        public IList<IWpfTextView> TextViews
        {
            get
            {
                _textViews.RemoveAll(textView => textView.IsClosed);
                return _textViews;
            }
        }

        [ImportingConstructor]
        public TextViewObserver()
        {
            this._textViews = new List<IWpfTextView>();
        }

        public void WpfTextViewCreated(IWpfTextView wpfTextView)
        {
            _textViews.Add(wpfTextView);
        }
    }
}
