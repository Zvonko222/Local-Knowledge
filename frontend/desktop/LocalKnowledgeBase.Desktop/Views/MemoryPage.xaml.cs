using System.Windows;
using System.Windows.Controls;

namespace LocalKnowledgeBase.Desktop.Views;

/// <summary>项目记忆页面。DataContext 是 MemoryViewModel，页面本身只做清空筛选这类视图接线。</summary>
public partial class MemoryPage : UserControl
{
    public MemoryPage()
    {
        InitializeComponent();
    }

    private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MemoryViewModel viewModel)
            viewModel.FilterText = string.Empty;
    }
}
