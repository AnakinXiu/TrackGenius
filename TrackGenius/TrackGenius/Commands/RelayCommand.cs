using System.Windows.Input;
using System;

namespace TrackGenius.UI.Commands;

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute)
        : this(execute, () => true)
    {
    }

    public bool CanExecute(object parameter) => _canExecute();

    public void Execute(object parameter) => _execute();

    public event EventHandler CanExecuteChanged = (sender, args) => { };

    // ReSharper disable once UnusedMember.Global
    public void RaiseCanExecuteChanged() => CanExecuteChanged(this, EventArgs.Empty);
}