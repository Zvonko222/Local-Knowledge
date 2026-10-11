using System.Windows;
using LocalKnowledgeBase.Desktop.ViewModels;

namespace LocalKnowledgeBase.Desktop;

/// <summary>
/// 主窗口外壳：侧边导航 + 内容区 + 状态栏。
/// ViewModel 由 App.xaml.cs 的容器注入，这里不做任何业务。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
