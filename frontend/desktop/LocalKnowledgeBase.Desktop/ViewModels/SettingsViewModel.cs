using System.Windows.Input;
using LocalKnowledgeBase.Desktop.Services;

namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>
/// 设置页面：数据目录配置 + API 服务地址配置。
/// 数据目录通过 API 持久化到后端；API 地址持久化在本机 client_settings.json。
/// </summary>
public sealed class SettingsViewModel : PageViewModelBase
{
    private readonly IKnowledgeApiClient _api;
    private readonly IDialogService _dialogs;
    private readonly IStatusReporter _status;

    private string _dataDirectory = string.Empty;
    private string _apiBaseUrl = ClientSettings.DefaultApiBaseUrl;
    private string _directoryStatusText = string.Empty;
    private string _apiStatusText = string.Empty;

    public SettingsViewModel(IKnowledgeApiClient api, IDialogService dialogs, IStatusReporter status)
    {
        _api = api;
        _dialogs = dialogs;
        _status = status;

        PickDirectoryCommand = new RelayCommand(PickDirectory);
        ApplyDirectoryCommand = new AsyncRelayCommand(ApplyDirectoryAsync, () => HasDataDirectory, onException: ShowFault);
        ApplyApiUrlCommand = new AsyncRelayCommand(ApplyApiUrlAsync, onException: ShowFault);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, onException: ShowFault);
    }

    public ICommand PickDirectoryCommand { get; }
    public ICommand ApplyDirectoryCommand { get; }
    public ICommand ApplyApiUrlCommand { get; }
    public ICommand TestConnectionCommand { get; }

    /// <summary>当前配置（或待应用）的数据目录。</summary>
    public string DataDirectory
    {
        get => _dataDirectory;
        private set
        {
            if (SetField(ref _dataDirectory, value))
                OnPropertyChanged(nameof(HasDataDirectory));
        }
    }

    public bool HasDataDirectory => DataDirectory.Length > 0;

    /// <summary>输入框中的 API 地址。</summary>
    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetField(ref _apiBaseUrl, value);
    }

    public string DirectoryStatusText
    {
        get => _directoryStatusText;
        private set => SetField(ref _directoryStatusText, value);
    }

    public string ApiStatusText
    {
        get => _apiStatusText;
        private set => SetField(ref _apiStatusText, value);
    }

    public override async void OnNavigatedTo() => await RefreshAsync();

    /// <summary>从 API 与本机配置文件刷新页面字段。</summary>
    public async Task RefreshAsync()
    {
        var directory = await _api.GetDataDirectoryAsync();
        DataDirectory = directory?.Configured == true && !string.IsNullOrEmpty(directory.Path)
            ? directory.Path
            : string.Empty;
        DirectoryStatusText = DataDirectory.Length > 0
            ? "当前数据目录已应用。"
            : "尚未配置数据目录，知识库数据将保存在 API 输出目录的 data 子目录。";

        ApiBaseUrl = ClientSettingsService.Load().ApiBaseUrl;
        ApiStatusText = $"当前地址：{ApiBaseUrl}";
    }

    private void PickDirectory()
    {
        var folder = _dialogs.PickFolder("选择知识库数据目录");
        if (folder is null)
            return;

        DataDirectory = folder;
        DirectoryStatusText = "目录已选择，点击“应用”生效。";
    }

    public async Task ApplyDirectoryAsync()
    {
        if (!HasDataDirectory)
            return;

        DirectoryStatusText = "正在应用数据目录…";
        if (!await _api.SetDataDirectoryAsync(DataDirectory))
        {
            DirectoryStatusText = _api.LastError ?? "设置数据目录失败，请检查 API 服务是否运行。";
            return;
        }

        DirectoryStatusText = "数据目录已立即生效。";
        _status.SetStatus($"数据目录：{DataDirectory}");
    }

    public async Task ApplyApiUrlAsync()
    {
        if (!TryApplyApiUrl(out _))
            return;

        ApiStatusText = $"API 地址已保存并应用：{ApiBaseUrl}";
        await ProbeConnectionAsync();
    }

    public async Task TestConnectionAsync()
    {
        if (!TryApplyApiUrl(out _))
            return;

        ApiStatusText = "正在测试连接…";
        await ProbeConnectionAsync();
    }

    /// <summary>校验并保存输入的 API 地址；非法时返回 false 并提示。</summary>
    private bool TryApplyApiUrl(out string appliedUrl)
    {
        appliedUrl = string.Empty;
        try
        {
            _api.SetBaseUrl(ApiBaseUrl);
        }
        catch (ArgumentException)
        {
            ApiStatusText = "API 地址无效，请输入完整的 http 或 https URL。";
            return false;
        }

        appliedUrl = _api.BaseUrl;
        ApiBaseUrl = appliedUrl;
        ClientSettingsService.Save(new ClientSettings { ApiBaseUrl = appliedUrl });
        return true;
    }

    private async Task ProbeConnectionAsync()
    {
        var connected = await _api.GetStatusAsync() is not null;
        _status.SetApiState(connected, connected ? $"API 已连接 · {_api.BaseUrl}" : $"API 未连接 · {_api.BaseUrl}");
        ApiStatusText = connected
            ? "连接成功，API 服务正常运行。"
            : $"连接失败：请确认 API 服务正在运行于 {_api.BaseUrl}";
    }

    private void ShowFault(Exception ex) => ApiStatusText = $"操作失败：{ex.Message}";
}
