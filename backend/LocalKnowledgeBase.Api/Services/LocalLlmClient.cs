using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalKnowledgeBase.Api.Services;

/// <summary>
/// OpenAI 兼容模型客户端（服务端版本）。
/// 供 /api/ai/answer 使用：检索知识库后调用模型生成回答。
/// 通过环境变量配置：LOCAL_KNOWLEDGE_BASE_LLM_URL、LOCAL_KNOWLEDGE_BASE_LLM_MODEL，
/// 云端 API 另需 LOCAL_KNOWLEDGE_BASE_LLM_KEY（以 Bearer 方式鉴权）。
/// </summary>
public sealed class LocalLlmClient
{
    public const string UrlEnvVar = "LOCAL_KNOWLEDGE_BASE_LLM_URL";
    public const string ModelEnvVar = "LOCAL_KNOWLEDGE_BASE_LLM_MODEL";
    public const string KeyEnvVar = "LOCAL_KNOWLEDGE_BASE_LLM_KEY";

    private readonly HttpClient _httpClient;

    public string BaseUrl { get; }
    public string Model { get; }

    public LocalLlmClient(string baseUrl, string model, string apiKey = "", HttpMessageHandler? handler = null)
    {
        BaseUrl = baseUrl;
        Model = model;
        _httpClient = handler is null
            ? new HttpClient
            {
                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromMinutes(3) // 模型生成可能较慢
            }
            : new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromMinutes(3)
            };
        if (!string.IsNullOrWhiteSpace(apiKey))
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
    }

    /// <summary>从环境变量读取配置；未配置（地址或模型名任一为空）返回 null。</summary>
    public static LocalLlmClient? FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable(UrlEnvVar);
        var model = Environment.GetEnvironmentVariable(ModelEnvVar);
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(model))
            return null;

        var key = Environment.GetEnvironmentVariable(KeyEnvVar) ?? string.Empty;

        // 只填了主机名（如 http://localhost:11434）时自动补 /v1，与桌面端行为一致
        var trimmed = url.Trim().TrimEnd('/');
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.PathAndQuery == "/"))
            trimmed += "/v1";

        return new LocalLlmClient(trimmed, model.Trim(), key);
    }

    /// <summary>发送一次补全请求。网络/协议问题抛异常，由控制器转成 502。</summary>
    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            stream = false
        };

        using var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
