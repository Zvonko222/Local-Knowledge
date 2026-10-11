using System.Windows;
using System.Windows.Controls;
using LocalKnowledgeBase.Desktop.ViewModels;

namespace LocalKnowledgeBase.Desktop.Views;

/// <summary>资料库页面。DataContext 是 DocumentsViewModel，页面本身只做拖拽这类视图层接线。</summary>
public partial class DocumentsPage : UserControl
{
    public DocumentsPage()
    {
        InitializeComponent();
    }

    private DocumentsViewModel? ViewModel => DataContext as DocumentsViewModel;

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        // 只接受资源管理器拖来的文件
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;

        if (ViewModel is { } viewModel)
            await viewModel.ImportFilesAsync(paths);
        e.Handled = true;
    }
}
