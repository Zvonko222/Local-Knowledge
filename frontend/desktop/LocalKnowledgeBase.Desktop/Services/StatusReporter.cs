using LocalKnowledgeBase.Desktop.ViewModels;

namespace LocalKnowledgeBase.Desktop.Services;

/// <summary>
/// 状态栏消息的报告接口。页面 ViewModel 通过它向主窗口状态栏汇报，
/// 避免页面直接引用 ShellViewModel。
/// </summary>
public interface IStatusReporter
{
    /// <summary>底部状态栏左侧显示的文本。</summary>
    string StatusMessage { get; }

    /// <summary>API 是否已连接（驱动状态栏与侧栏的连接指示灯）。</summary>
    bool IsApiConnected { get; }

    /// <summary>API 连接详情，如 “API 已连接 · https://localhost:7047”。</summary>
    string ApiDetail { get; }

    void SetStatus(string message);

    void SetApiState(bool connected, string? detail = null);
}

/// <summary>默认实现；ShellViewModel 直接把它暴露给主窗口绑定。</summary>
public sealed class StatusReporter : ViewModelBase, IStatusReporter
{
    private string _statusMessage = "正在启动…";
    private bool _isApiConnected;
    private string _apiDetail = "API 未连接";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsApiConnected
    {
        get => _isApiConnected;
        private set => SetField(ref _isApiConnected, value);
    }

    public string ApiDetail
    {
        get => _apiDetail;
        private set => SetField(ref _apiDetail, value);
    }

    public void SetStatus(string message) => StatusMessage = message;

    public void SetApiState(bool connected, string? detail = null)
    {
        IsApiConnected = connected;
        ApiDetail = detail ?? (connected ? "API 已连接" : "API 未连接");
    }
}
