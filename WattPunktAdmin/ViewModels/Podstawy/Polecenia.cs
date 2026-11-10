using System.Windows.Input;

namespace WattPunktAdmin.ViewModels;

public class RelayCommand(Action<object?> wykonaj, Predicate<object?>? mozna = null) : ICommand
{
    public RelayCommand(Action wykonaj, Func<bool>? mozna = null)
        : this(_ => wykonaj(), mozna is null ? null : _ => mozna()) { }

    public bool CanExecute(object? parametr) => mozna?.Invoke(parametr) ?? true;
    public void Execute(object? parametr) => wykonaj(parametr);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

// Polecenie asynchroniczne: w trakcie wykonywania jest nieaktywne, wiec nie da sie go uruchomic dwa razy
public class AsyncRelayCommand(Func<Task> wykonaj, Func<bool>? mozna = null) : ICommand
{
    private bool trwa;

    public bool CanExecute(object? parametr) => !trwa && (mozna?.Invoke() ?? true);

    public async void Execute(object? parametr)
    {
        if (!CanExecute(parametr)) return;
        trwa = true;
        CommandManager.InvalidateRequerySuggested();
        try { await wykonaj(); }
        finally
        {
            trwa = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
