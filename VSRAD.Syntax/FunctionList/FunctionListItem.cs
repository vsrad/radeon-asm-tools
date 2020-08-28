using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.FunctionList
{
    public class FunctionListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public RadAsmTokenType Type { get; }
        public string Text { get; }
        public int LineNumber { get; }

        private bool _isCurrentWorkingItem;
        public bool IsCurrentWorkingItem
        {
            get => _isCurrentWorkingItem;
            set => OnPropertyChanged(ref _isCurrentWorkingItem, value);
        }

        public FunctionListItem(RadAsmTokenType type, string text, int lineNumber)
        {
            Type = type;
            Text = text;
            LineNumber = lineNumber + 1;
            _isCurrentWorkingItem = false;
        }

        private void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            RaisePropertyChanged(propertyName);
        }

        private void RaisePropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    }
}
