using System;
using System.Windows.Input;

namespace VSRAD.Package.Utils
{
    // Source: https://msdn.microsoft.com/en-us/magazine/dd419663.aspx
    public sealed class WpfDelegateCommand : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; CommandManager.InvalidateRequerySuggested(); }
        }

        private readonly Action<object> _execute;

        public WpfDelegateCommand(Action<object> execute, bool isEnabled = true)
        {
            _execute = execute;
            _isEnabled = isEnabled;
        }

        public bool CanExecute(object parameter) => _isEnabled;

        public void Execute(object parameter) => _execute(parameter);
    }
}
