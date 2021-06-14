using System;
using System.Windows.Input;

namespace VSRAD.Syntax.FunctionList
{
    public interface INoParameterCommand
    {
        event EventHandler CanExecuteChanged;
        bool CanExecute();
        void Execute();
    }

    public class NoParameterCommand : ICommand, INoParameterCommand
    {
        private readonly Action _executeDelegate;
        private readonly Func<bool> _canExecuteDelegate;

        public event EventHandler CanExecuteChanged = null;

        public NoParameterCommand(Action execute)
        {
            _executeDelegate = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecuteDelegate = () => true;
        }
        public NoParameterCommand(Action execute, Func<bool> canExecute)
        {
            _executeDelegate = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecuteDelegate = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
        }

        public bool CanExecute() => _canExecuteDelegate();

        public void Execute() => _executeDelegate();

        public bool CanExecute(object parameter) => CanExecute();

        public void Execute(object parameter) => Execute();
    }
}
