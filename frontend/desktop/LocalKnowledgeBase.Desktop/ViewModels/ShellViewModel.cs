using System.Collections.Generic;
using System.Windows.Input;
using LocalKnowledgeBase.Desktop.Services;

namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>
/// 主窗口（Shell）的 ViewModel：只负责侧边导航、当前页面与状态栏。
/// 每个页面的业务逻辑在各自的 PageViewModelBase 子类中。
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private readonly IKnowledgeApiClient _api;

    /// <summary>侧边栏一项：图标 + 标题 + 对应页面 ViewModel。</summary>
    public sealed class NavItem
    {
        public required string Glyph { get; init; }
        public required string Title { get; init; }
        public required PageViewModelBase Page { get; init; }

        public override string ToString() => Title;
    }

    private NavItem? _selectedNavItem;

    public ShellViewModel(
        IKnowledgeApiClient api,
        StatusReporter status,
        DocumentsViewModel documents,
        MemoryViewModel memory,
        ChatViewModel chat,
        SearchViewModel search,
        SettingsViewModel settings)
    {
        _api = api;
        Status = status;

        // 新增页面：在这里加一个 NavItem，然后在 App.xaml 中补一条 ViewModel → View 的 DataTemplate。
        NavItems = new List<NavItem>
        {
            new() { Glyph = "\uE8F1", Title = "资料库", Page = documents },
            new() { Glyph = "\uE70B", Title = "项目记忆", Page = memory },
            new() { Glyph = "\uE8BD", Title = "问答", Page = chat },
            new() { Glyph = "\uE721", Title = "搜索", Page = search },
            new() { Glyph = "\uE713", Title = "设置", Page = settings }
        };

        _selectedNavItem = NavItems[0];
        CurrentPage = NavItems[0].Page;
    }

    /// <summary>侧边栏导航项（顺序即显示顺序）。</summary>
    public IReadOnlyList<NavItem> NavItems { get; }

    /// <summary>侧边栏当前选中项；变更会切换内容区并通知页面。</summary>
    public NavItem? SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            if (!SetField(ref _selectedNavItem, value) || value is null)
                return;

            CurrentPage = value.Page;
            OnPropertyChanged(nameof(CurrentPage));
            CurrentPage.OnNavigatedTo();
        }
    }

    /// <summary>内容区当前显示的页面 ViewModel（由 App.xaml 中的 DataTemplate 映射为视图）。</summary>
    public PageViewModelBase CurrentPage { get; private set; }

    /// <summary>状态栏数据源（底部状态栏 + 侧栏 API 卡片共用）。</summary>
    public StatusReporter Status { get; }

    /// <summary>启动时探测一次 API 连接，结果写入状态栏，并触发首屏页面加载。</summary>
    public async Task InitializeAsync()
    {
        var status = await _api.GetStatusAsync();
        if (status is not null)
        {
            Status.SetApiState(true, $"API 已连接 · {_api.BaseUrl}");
            Status.SetStatus("服务就绪");
        }
        else
        {
            Status.SetApiState(false, $"API 未连接 · {_api.BaseUrl}");
            Status.SetStatus("API 未连接，请在“设置”页检查服务地址，或先启动 API 服务。");
        }

        // 默认页在构造时选中，从未走过导航切换；这里补一次首屏数据加载
        CurrentPage.OnNavigatedTo();
    }
}
