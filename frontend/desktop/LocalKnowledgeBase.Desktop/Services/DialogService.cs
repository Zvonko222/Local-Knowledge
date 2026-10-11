using System.IO;
using System.Windows;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace LocalKnowledgeBase.Desktop.Services;

/// <summary>
/// 通用对话框抽象（选文件、选目录、确认框）。
/// 抽出来是为了让 ViewModel 不直接依赖 UI 类型，可在测试中替换。
/// </summary>
public interface IDialogService
{
    /// <summary>选择一个要打开的文件，取消返回 null。</summary>
    string? PickOpenFile(string title, string filter, string? initialDirectory = null);

    /// <summary>选择一个文件夹，取消返回 null。</summary>
    string? PickFolder(string title);

    /// <summary>确认对话框，返回用户是否点了“是”。</summary>
    bool Confirm(string title, string message);
}

/// <summary>基于 WPF 内置对话框的实现。</summary>
public sealed class DialogService : IDialogService
{
    public string? PickOpenFile(string title, string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            InitialDirectory = initialDirectory is { Length: > 0 } && Directory.Exists(initialDirectory)
                ? initialDirectory
                : null
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        // .NET 8+ 自带 OpenFolderDialog，无需再引入 WinForms 的 FolderBrowserDialog
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;
    }
}
