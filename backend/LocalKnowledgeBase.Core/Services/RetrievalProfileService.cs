using System.Text.Json;

namespace LocalKnowledgeBase.Core.Services;

/// <summary>
/// 检索预算档位：控制每次检索注入上下文的规模（条数/单条摘要/总字符预算），
/// 从面控制单次检索的 token 消耗。TokenWeight 为相对标准档的近似 token 比重
/// （中文约 1 字符 ≈ 1 token 量级，实际取决于分词器）。
/// </summary>
public sealed record RetrievalProfile(string Preset, int MaxResults, int SnippetLength, int MaxTotalChars, double TokenWeight)
{
    /// <summary>轻量：省 token，适合简单问题（约 0.4×）。</summary>
    public static readonly RetrievalProfile Light = new("light", 3, 400, 1500, 0.4);

    /// <summary>标准：默认档（1×）。</summary>
    public static readonly RetrievalProfile Standard = new("standard", 6, 800, 4000, 1.0);

    /// <summary>深度：详尽检索，适合综合任务（约 2×）。</summary>
    public static readonly RetrievalProfile Deep = new("deep", 10, 1200, 8000, 2.0);
}

/// <summary>
/// 检索预算档位的持久化与读取：存于
/// %APPDATA%\LocalKnowledgeBase\retrieval_profile.json。
/// 所有检索入口（桌面搜索/问答、MCP、api/ai）在请求未显式指定参数时
/// 自动套用当前档位；AI 可经 MCP 工具运行时切换。
/// </summary>
public sealed class RetrievalProfileService
{
    public static readonly string DefaultDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LocalKnowledgeBase");

    private readonly string _filePath;
    private readonly object _lock = new();
    private RetrievalProfile _current;

    /// <summary>appDataDirectory 仅供测试注入；生产使用默认 %APPDATA% 路径。</summary>
    public RetrievalProfileService(string? appDataDirectory = null)
    {
        _filePath = Path.Combine(appDataDirectory ?? DefaultDirectory, "retrieval_profile.json");
        _current = Load();
    }

    /// <summary>当前生效的档位。</summary>
    public RetrievalProfile Get()
    {
        lock (_lock)
            return _current;
    }

    /// <summary>按档位名切换并持久化；未知名称回落到 standard。</summary>
    public RetrievalProfile Set(string preset)
    {
        lock (_lock)
        {
            _current = Resolve(preset);
            Save(_current);
            return _current;
        }
    }

    /// <summary>按档位名解析预设；不区分大小写，未知名称回落 standard。</summary>
    public static RetrievalProfile Resolve(string? preset) => preset?.Trim().ToLowerInvariant() switch
    {
        "light" => RetrievalProfile.Light,
        "deep" => RetrievalProfile.Deep,
        _ => RetrievalProfile.Standard
    };

    private RetrievalProfile Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return RetrievalProfile.Standard;

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return RetrievalProfile.Standard;

            var stored = JsonSerializer.Deserialize<StoredProfile>(json);
            return Resolve(stored?.Preset);
        }
        catch
        {
            return RetrievalProfile.Standard;
        }
    }

    private void Save(RetrievalProfile profile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(new StoredProfile(profile.Preset),
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    private sealed record StoredProfile(string Preset);
}
