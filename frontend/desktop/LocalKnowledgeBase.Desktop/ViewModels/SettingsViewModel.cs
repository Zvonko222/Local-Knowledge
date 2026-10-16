using System.IO;
using System.IO.Compression;
using System.Windows.Input;
using LocalKnowledgeBase.Desktop.Services;

namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>
/// 设置页面：数据目录配置 + API 服务地址配置 + 界面语言/背景/检索预算。
/// 数据目录通过 API 持久化到后端；其余本机设置持久化在 client_settings.json。
/// </summary>
public sealed class SettingsViewModel : PageViewModelBase
{
    private readonly IKnowledgeApiClient _api;
    private readonly ILlmClient _llm;
    private readonly IDialogService _dialogs;
    private readonly IStatusReporter _status;
    private readonly ILocalization _localization;

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
    private string _languageStatusText = string.Empty;
    private string _backgroundStatusText = string.Empty;
    private string _languageLabel = SettingsViewModel.LanguageZhLabel;
    private string _backgroundLabel = SettingsViewModel.BackgroundDefaultLabel;

    public SettingsViewModel(IKnowledgeApiClient api, ILlmClient llm, IDialogService dialogs, IStatusReporter status,
        ILocalization localization)
    {
        _api = api;
        _llm = llm;
        _dialogs = dialogs;
        _status = status;
        _localization = localization;

        PickDirectoryCommand = new RelayCommand(PickDirectory);
        OpenDataDirectoryCommand = new RelayCommand(OpenDataDirectory, () => HasDataDirectory);
        BackupDataCommand = new AsyncRelayCommand(BackupDataAsync, () => HasDataDirectory, onException: ShowFault);
        ApplyDirectoryCommand = new AsyncRelayCommand(ApplyDirectoryAsync, () => HasDataDirectory, onException: ShowFault);
        ApplyApiUrlCommand = new AsyncRelayCommand(ApplyApiUrlAsync, onException: ShowFault);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, onException: ShowFault);
        ApplyLlmSettingsCommand = new RelayCommand(ApplyLlmSettings);
        ApplyProfileCommand = new AsyncRelayCommand(ApplyProfileAsync, onException: ex => ProfileStatusText = $"切换失败：{ex.Message}");
        ApplyLanguageCommand = new RelayCommand(ApplyLanguage);
        ApplyBackgroundCommand = new RelayCommand(ApplyBackground);
        TestLlmCommand = new AsyncRelayCommand(TestLlmAsync, onException: ex => LlmStatusText = $"操作失败：{ex.Message}");
    }

    public ICommand ApplyLanguageCommand { get; }
    public ICommand ApplyBackgroundCommand { get; }

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

    /// <summary>服务商预设：选择后自动填充服务地址与示例模型（以 DeepSeek 填写方式为准）。</summary>
    public List<string> ProviderOptions { get; } = new()
    {
        "DeepSeek", "智谱 GLM", "OpenAI", "Ollama（本地）", "自定义"
    };

    private string _selectedProvider = "自定义";
    public string SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetField(ref _selectedProvider, value))
                ApplyProviderPreset(value);
        }
    }

    private void ApplyProviderPreset(string provider)
    {
        switch (provider)
        {
            case "DeepSeek":
                LlmBaseUrl = "https://api.deepseek.com/v1";
                LlmModel = "deepseek-chat";
                break;
            case "智谱 GLM":
                LlmBaseUrl = "https://open.bigmodel.cn/api/paas/v4";
                LlmModel = "glm-4.7";
                break;
            case "OpenAI":
                LlmBaseUrl = "https://api.openai.com/v1";
                LlmModel = "gpt-4o-mini";
                break;
            case "Ollama（本地）":
                LlmBaseUrl = "http://localhost:11434/v1";
                LlmModel = "qwen2.5:7b";
                break;
            // 自定义：保留用户已填内容
        }
    }

    private void InferProviderFromBaseUrl(string baseUrl)
    {
        _selectedProvider = baseUrl.ToLowerInvariant() switch
        {
            var u when u.Contains("deepseek") => "DeepSeek",
            var u when u.Contains("bigmodel") => "智谱 GLM",
            var u when u.Contains("openai.com") => "OpenAI",
            var u when u.Contains("11434") => "Ollama（本地）",
            _ => "自定义"
        };
        OnPropertyChanged(nameof(SelectedProvider));
    }

    public string ProfileStatusText
    {
        get => _profileStatusText;
        private set => SetField(ref _profileStatusText, value);
    }

    public const string ProfileLightLabel = "轻量（省 token，约 0.4×）";
    public const string ProfileStandardLabel = "标准（约 1×）";
    public const string ProfileDeepLabel = "深度（约 2×，更详尽）";

    public const string LanguageZhLabel = "中文";
    public const string LanguageEnLabel = "English";
    public const string BackgroundDefaultLabel = "默认（米白 + 墨绿侧栏）";
    public const string BackgroundCoolLabel = "冷灰蓝";
    public const string BackgroundWarmLabel = "暖米色";

    public List<string> LanguageOptions { get; } = new() { LanguageZhLabel, LanguageEnLabel };

    public string LanguageLabel
    {
        get => _languageLabel;
        set => SetField(ref _languageLabel, value);
    }

    public List<string> BackgroundOptions { get; } = new()
    {
        BackgroundDefaultLabel, BackgroundCoolLabel, BackgroundWarmLabel
    };

    public string BackgroundLabel
    {
        get => _backgroundLabel;
        set => SetField(ref _backgroundLabel, value);
    }

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

    public string LanguageStatusText
    {
        get => _languageStatusText;
        private set => SetField(ref _languageStatusText, value);
    }

    public string BackgroundStatusText
    {
        get => _backgroundStatusText;
        private set => SetField(ref _backgroundStatusText, value);
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
        InferProviderFromBaseUrl(LlmBaseUrl);

        var currentSettings = ClientSettingsService.Load();
        LanguageLabel = currentSettings.Language == "en" ? LanguageEnLabel : LanguageZhLabel;
        BackgroundLabel = currentSettings.Background switch
        {
            "cool" => BackgroundCoolLabel,
            "warm" => BackgroundWarmLabel,
            _ => BackgroundDefaultLabel
        };
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

    /// <summary>切换检索预算档位并持久化到 API（对所有检索入口生效）。异步执行，不阻塞 UI。</summary>
    public async Task ApplyProfileAsync()
    {
        try
        {
            var profile = await _api.SetProfileAsync(LabelToPreset(ProfileLabel));
            if (profile is null)
            {
                ProfileStatusText = "切换失败，请检查 API 服务是否运行。";
                return;
            }

            ProfileStatusText = _localization.Culture == "en"
                ? $"Switched to {profile.Preset}: up to {profile.MaxTotalChars} chars / {profile.MaxResults} results per retrieval (about {profile.TokenWeight}× tokens), applies everywhere."
                : $"已切换到 {profile.Preset}：单次检索 ≤ {profile.MaxTotalChars} 字符 / {profile.MaxResults} 条" +
                  $"（token 比重约 {profile.TokenWeight}×），对所有检索入口生效。";
        }
        catch (Exception ex)
        {
            ProfileStatusText = $"切换失败：{ex.Message}";
        }
    }

    /// <summary>切换界面语言：立即生效（界面文字随 DynamicResource 更新）并持久化。</summary>
    public void ApplyLanguage()
    {
        var culture = LanguageLabel == LanguageEnLabel ? "en" : "zh";
        _localization.SetCulture(culture);

        var settings = ClientSettingsService.Load();
        settings.Language = culture;
        ClientSettingsService.Save(settings);

        LanguageStatusText = culture == "en"
            ? "Language switched to English."
            : "界面语言已切换。";
    }

    /// <summary>保存背景调色板（重启应用后生效）。</summary>
    public void ApplyBackground()
    {
        var background = BackgroundLabel switch
        {
            BackgroundCoolLabel => "cool",
            BackgroundWarmLabel => "warm",
            _ => "default"
        };

        var settings = ClientSettingsService.Load();
        settings.Background = background;
        ClientSettingsService.Save(settings);

        BackgroundStatusText = _localization.Culture == "en"
            ? $"Saved '{BackgroundLabel}'. Takes effect after restart."
            : $"已保存“{BackgroundLabel}”，重启应用后生效。";
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
