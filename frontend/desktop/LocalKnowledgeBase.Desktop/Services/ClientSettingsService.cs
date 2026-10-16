using System.IO;
using System.Text.Json;

namespace LocalKnowledgeBase.Desktop.Services;

public sealed class ClientSettings
{
    public const string DefaultApiBaseUrl = "https://localhost:7047";
    public const string DefaultLlmBaseUrl = "http://localhost:11434/v1";

    public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;

    /// <summary>模型服务地址（OpenAI 兼容接口），用于 AI 问答。云端 API 与本地服务均可。</summary>
    public string LlmBaseUrl { get; set; } = DefaultLlmBaseUrl;

    /// <summary>模型名称，例如 glm-4.7、deepseek-chat、qwen2.5:7b；留空表示未配置。</summary>
    public string LlmModel { get; set; } = string.Empty;

    /// <summary>API 密钥：云端 API 必填（以 Bearer 方式鉴权），本地服务（Ollama 等）留空。</summary>
    public string LlmApiKey { get; set; } = string.Empty;

    /// <summary>界面语言："zh"（中文，默认）或 "en"（English）。</summary>
    public string Language { get; set; } = "zh";

    /// <summary>背景调色板："default"（米白+墨绿）/ "cool"（冷灰蓝）/ "warm"（暖米），重启后生效。</summary>
    public string Background { get; set; } = "default";
}

public static class ClientSettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LocalKnowledgeBase");

    private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "client_settings.json");

    public static ClientSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new ClientSettings();

            var json = File.ReadAllText(SettingsFile);
            var settings = JsonSerializer.Deserialize<ClientSettings>(json);
            if (settings == null ||
                !Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out var apiUri) ||
                (apiUri.Scheme != Uri.UriSchemeHttp && apiUri.Scheme != Uri.UriSchemeHttps))
            {
                return new ClientSettings();
            }

            return settings;
        }
        catch
        {
            return new ClientSettings();
        }
    }

    public static void Save(ClientSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(SettingsFile, json);
    }
}
