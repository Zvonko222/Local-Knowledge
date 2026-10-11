using System.IO;
using System.IO.Compression;
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
    private string _llmBaseUrl = ClientSettings.DefaultLlmBaseUrl;
    private string _llmModel = string.Empty;
    private string _llmApiKey = string.Empty;
    private string _profileLabel = ProfileStandardLabel;
    private string _profileStatusText = string.Empty;
    private string _directoryStatusText = string.Empty;
    private string _apiStatusText = string.Empty;
    private string _llmStatusText = string.Empty;

    public SettingsViewModel(IKnowledgeApiClient api, ILlmClient llm, IDialogService dialogs, IStatusReporter status)
    {
        _api = api;
        _llm = llm;
        _dialogs = dialogs;
        _status = status;

        PickDirectoryCommand = new RelayCommand(PickDirectory);
        OpenDataDirectoryCommand = new RelayCommand(OpenDataDirectory, () => HasDataDirectory);
        BackupDataCommand = new AsyncRelayCommand(BackupDataAsync, () => HasDataDirectory, onException: ShowFault);
        ApplyDirectoryCommand = new AsyncRelayCommand(ApplyDirectoryAsync, () => HasDataDirectory, onException: ShowFault);
        ApplyApiUrlCommand = new AsyncRelayCommand(ApplyApiUrlAsync, onException: ShowFault);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, onException: ShowFault);
        ApplyLlmSettingsCommand = new RelayCommand(ApplyLlmSettings);
        ApplyProfileCommand = new RelayCommand(ApplyProfile);
        TestLlmCommand = new AsyncRelayCommand(TestLlmAsync, onException: ex => LlmStatusText = $"操作失败：{ex.Message}");
    }

    private readonly ILlmClient _llm;

    public ICommand PickDirectoryCommand { get; }
    public ICommand OpenDataDirectoryCommand { get; }
    public ICommand BackupDataCommand { get; }
    public ICommand ApplyDirectoryCommand { get; }
    public ICommand ApplyApiUrlCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand ApplyLlmSettingsCommand { get; }
    public ICommand ApplyProfileCommand { get; }
    public ICommand TestLlmCommand { get; }

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

    /// <summary>本地模型服务地址（OpenAI 兼容接口）。</summary>
    public string LlmBaseUrl
    {
        get => _llmBaseUrl;
        set => SetField(ref _llmBaseUrl, value);
    }

    /// <summary>模型名称，如 glm-4.7、deepseek-chat、qwen2.5:7b。</summary>
    public string LlmModel
    {
        get => _llmModel;
        set => SetField(ref _llmModel, value);
    }

    /// <summary>API 密钥：云端 API 必填，本地服务留空。</summary>
    public string LlmApiKey
    {
        get => _llmApiKey;
        set => SetField(ref _llmApiKey, value);
    }

    /// <summary>检索预算档位标签（轻量/标准/深度）。</summary>
    public string ProfileLabel
    {
        get => _profileLabel;
        set => SetField(ref _profileLabel, value);
    }

    public List<string> ProfileOptions { get; } = new()
    {
        ProfileLightLabel, ProfileStandardLabel, ProfileDeepLabel
    };

    public string ProfileStatusText
    {
        get => _profileStatusText;
        private set => SetField(ref _profileStatusText, value);
    }

    public const string ProfileLightLabel = "轻量（省 token，约 0.4×）";
    public const string ProfileStandardLabel = "标准（约 1×）";
    public const string ProfileDeepLabel = "深度（约 2×，更详尽）";

    private static string LabelToPreset(string label) => label switch
    {
        ProfileLightLabel => "light",
        ProfileDeepLabel => "deep",
        _ => "standard"
    };

    private static string PresetToLabel(string preset) => preset switch
    {
        "light" => ProfileLightLabel,
        "deep" => ProfileDeepLabel,
        _ => ProfileStandardLabel
    };

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

    public string LlmStatusText
    {
        get => _llmStatusText;
        private set => SetField(ref _llmStatusText, value);
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

        var settings = ClientSettingsService.Load();
        ApiBaseUrl = settings.ApiBaseUrl;
        ApiStatusText = $"当前地址：{ApiBaseUrl}";
        LlmBaseUrl = settings.LlmBaseUrl;
        LlmModel = settings.LlmModel;
        LlmApiKey = settings.LlmApiKey;

        var profile = await _api.GetProfileAsync();
        ProfileLabel = PresetToLabel(profile?.Preset ?? "standard");
        ProfileStatusText = profile is null
            ? "未读取到检索预算档位（检查 API 服务）。"
            : $"当前档位：{profile.Preset}｜单次检索 ≤ {profile.MaxTotalChars} 字符 / {profile.MaxResults} 条（token 比重约 {profile.TokenWeight}×）";

        LlmStatusText = LlmModel.Length == 0
            ? "未配置模型，问答页暂不可用。填写 OpenAI 兼容地址与模型名（云端 API 需同时填密钥）。"
            : $"当前模型：{LlmModel}";
    }

    private void PickDirectory()
    {
        var folder = _dialogs.PickFolder("选择知识库数据目录");
        if (folder is null)
            return;

        DataDirectory = folder;
        DirectoryStatusText = "目录已选择，点击“应用”生效。";
    }

    /// <summary>在资源管理器中打开当前数据目录，便于查看内容与手动备份。</summary>
    private void OpenDataDirectory()
    {
        if (HasDataDirectory)
            _dialogs.OpenInExplorer(DataDirectory);
    }

    /// <summary>一键备份：把整个数据目录压缩为 ZIP（含 documents/memory/raw_files/vectors）。</summary>
    public async Task BackupDataAsync()
    {
        var defaultName = $"knowledge-base-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        var path = _dialogs.PickSaveFile("备份数据目录", "ZIP 压缩包|*.zip", defaultName);
        if (path is null)
            return;

        DirectoryStatusText = "正在备份（文档较多时可能需要一些时间）…";
        await Task.Run(() =>
        {
            if (File.Exists(path))
                File.Delete(path);
            ZipFile.CreateFromDirectory(DataDirectory, path, CompressionLevel.Optimal, includeBaseDirectory: false);
        });
        DirectoryStatusText = $"已备份到：{path}";
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

    /// <summary>校验并保存输入的 API 地址；非法时返回 false 并提示。保存时保留其他配置项。</summary>
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

        var settings = ClientSettingsService.Load();
        settings.ApiBaseUrl = appliedUrl;
        ClientSettingsService.Save(settings);
        return true;
    }

    /// <summary>应用本地模型配置（校验地址并保存到 client_settings.json）。</summary>
    private void ApplyLlmSettings()
    {
        try
        {
            _llm.Configure(LlmBaseUrl, LlmModel, LlmApiKey);
        }
        catch (ArgumentException)
        {
            LlmStatusText = "模型服务地址无效，请输入完整的 http 或 https URL。";
            return;
        }

        LlmBaseUrl = _llm.BaseUrl;
        LlmModel = _llm.Model;

        var settings = ClientSettingsService.Load();
        settings.LlmBaseUrl = _llm.BaseUrl;
        settings.LlmModel = _llm.Model;
        settings.LlmApiKey = LlmApiKey.Trim();
        ClientSettingsService.Save(settings);

        LlmStatusText = LlmModel.Length == 0
            ? "已保存（模型名为空，问答页暂不可用）。"
            : $"已保存并生效：{LlmModel}";
    }

    /// <summary>切换检索预算档位并持久化到 API（对所有检索入口生效）。</summary>
    public void ApplyProfile()
    {
        try
        {
            var profile = _api.SetProfileAsync(LabelToPreset(ProfileLabel)).GetAwaiter().GetResult();
            if (profile is null)
            {
                ProfileStatusText = "切换失败，请检查 API 服务是否运行。";
                return;
            }

            ProfileStatusText = $"已切换到 {profile.Preset}：单次检索 ≤ {profile.MaxTotalChars} 字符 / {profile.MaxResults} 条" +
                                $"（token 比重约 {profile.TokenWeight}×），对所有检索入口生效。";
        }
        catch (Exception ex)
        {
            ProfileStatusText = $"切换失败：{ex.Message}";
        }
    }

    private async Task TestLlmAsync()
    {
        ApplyLlmSettings();
        LlmStatusText = "正在测试连接…";
        var connected = await _llm.TestConnectionAsync();
        LlmStatusText = connected
            ? $"连接成功（{_llm.BaseUrl}）。"
            : $"无法连接 {_llm.BaseUrl}。请检查地址、模型名与密钥（云端 API 需要密钥）。";
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
