using System.Windows.Input;
using System;
using JetBrains.Annotations;

namespace TrackGenius.UI.Commands;


    public class RelayCommand<TParameter> : ICommand
    {
        [NotNull]
        private readonly Action<TParameter> _execute;
        [NotNull]
        private readonly Func<TParameter, bool> _canExecute;

        public RelayCommand(Action<TParameter> execute, Func<TParameter, bool> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Action<TParameter> execute)
            : this(execute, parameter => true)
        { }

        public bool CanExecute([NotNull] object parameter) => _canExecute((TParameter)parameter);

        public void Execute([NotNull] object parameter) => _execute((TParameter)parameter);

        public event EventHandler CanExecuteChanged = (sender, args) => { };

        // ReSharper disable once UnusedMember.Global
        public void RaiseCanExecuteChanged() => CanExecuteChanged(this, EventArgs.Empty);
    }