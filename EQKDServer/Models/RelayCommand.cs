using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EQKDServer.Models
{
    public class RelayCommand<T> : ICommand
    {
        private Action<T> _execute;
        private Predicate<T> _canExcecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    
        public RelayCommand(Action<T> excec, Predicate<T> canExec)
        {
            _execute = excec;
            _canExcecute = canExec;
        }

        public RelayCommand(Action<T> act) : this(act, null)
        {

        }

        public bool CanExecute(object parameter)
        {
            return _canExcecute == null ? true : _canExcecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            _execute.Invoke((T)parameter);
        }
    }

    //public class RelayCommand : ICommand
    //{
    //    private Action<object> _execute;
    //    private Predicate<object> _canExcecute;

    //    public event EventHandler CanExecuteChanged
    //    {
    //        add { CommandManager.RequerySuggested += value; }
    //        remove { CommandManager.RequerySuggested -= value; }
    //    }

    //    public RelayCommand(Action<object> excec, Predicate<object> canExec)
    //    {
    //        _execute = excec;
    //        _canExcecute = canExec;
    //    }

    //    public RelayCommand(Action<object> act) : this(act, null)
    //    {

    //    }

    //    public bool CanExecute(object parameter)
    //    {
    //        return _canExcecute == null ? true : _canExcecute(parameter);
    //    }

    //    public void Execute(object parameter)
    //    {
    //        _execute.Invoke(parameter);
    //    }
    //}

}
