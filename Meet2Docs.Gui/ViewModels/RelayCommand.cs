﻿using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Meet2Docs.Gui.ViewModels;
public sealed class RelayCommand : ICommand
{
    private readonly Func<object?, Task> _executeAsync;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
    { _executeAsync = executeAsync; _canExecute = canExecute; }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public event EventHandler? CanExecuteChanged;
    public async void Execute(object? parameter) => await _executeAsync(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}