using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(TextViewObserver))]
    internal sealed class TextViewObserver
    {
        private static readonly object lockObj = new object();
        private readonly List<IWpfTextView> _textViews;
        private int _count;
        public IList<IWpfTextView> TextViews
        {
            get
            {
                lock (lockObj)
                {
                    RemoveUnusedTextViews();
                    return _textViews;
                }
            }
        }

        [ImportingConstructor]
        public TextViewObserver()
        {
            this._textViews = new List<IWpfTextView>();
            this._count = 0;
        }

        public void WpfTextViewCreated(IWpfTextView wpfTextView)
        {
            lock (lockObj)
            {
                _textViews.Add(wpfTextView);
                _count++;
                if (_count >= 15)
                    RemoveUnusedTextViews();
            }
        }

        private void RemoveUnusedTextViews() => 
            _textViews.RemoveAll(textView => textView == null || textView.IsClosed);
    }
}
