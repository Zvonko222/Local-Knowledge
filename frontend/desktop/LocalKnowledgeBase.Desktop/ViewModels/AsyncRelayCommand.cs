using System.Diagnostics;
using System.Windows.Input;

namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>
/// 异步命令：执行期间 IsRunning 为 true（命令自动禁用，按钮随之置灰），
/// 未捕获的异常交给 onException 回调，避免 async void 崩溃进程。
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onException;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onException = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onException = onException;
    }

    /// <summary>命令当前是否正在执行（执行中按钮自动禁用）。</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            _isRunning = value;
            RaiseCanExecuteChanged();
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !IsRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        IsRunning = true;
        try
        {
            await _execute();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AsyncRelayCommand] 未处理异常: {ex}");
            _onException?.Invoke(ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
