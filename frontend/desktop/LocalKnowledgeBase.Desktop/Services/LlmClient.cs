using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalKnowledgeBase.Desktop.Services;

/// <summary>
/// 本地大模型客户端抽象（AI 问答用）。
/// </summary>
public interface ILlmClient
{
    string BaseUrl { get; }
    string Model { get; }
    string ApiKey { get; }

    /// <summary>切换地址、模型与密钥。地址非法时抛 <see cref="ArgumentException"/>。</summary>
    void Configure(string baseUrl, string model, string apiKey = "");

    /// <summary>发送一次补全请求，返回模型回答文本。失败抛异常，由调用方展示。</summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);

    /// <summary>探测模型服务是否在线（GET /models）。</summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// OpenAI 兼容接口实现：云端 API（GLM、DeepSeek、OpenAI 等，需密钥）与
/// 本地服务（Ollama、LM Studio 等，免密钥）通用。请求只发往用户配置的地址。
/// </summary>
public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private string _apiKey = string.Empty;

    public string BaseUrl { get; private set; }
    public string Model { get; private set; }
    public string ApiKey => _apiKey;

    public OpenAiCompatibleLlmClient(string baseUrl, string model, string apiKey = "", HttpMessageHandler? handler = null)
    {
        (BaseUrl, Model) = Normalize(baseUrl, model);
        _apiKey = (apiKey ?? string.Empty).Trim();
        // handler 参数供单元测试注入桩实现；生产代码传 null 使用默认栈
        _httpClient = handler is null
            ? new HttpClient { Timeout = TimeSpan.FromMinutes(3) } // 模型生成可能较慢
            : new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(3) };
        _httpClient.BaseAddress = new Uri(BaseUrl);
        ApplyAuth();
    }

    public void Configure(string baseUrl, string model, string apiKey = "")
    {
        (BaseUrl, Model) = Normalize(baseUrl, model);
        _apiKey = (apiKey ?? string.Empty).Trim();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        ApplyAuth();
    }

    private void ApplyAuth()
    {
        // 云端 API 用 Bearer 鉴权；本地服务（Ollama 等）密钥为空则不带该头
        _httpClient.DefaultRequestHeaders.Authorization = _apiKey.Length > 0
            ? new AuthenticationHeaderValue("Bearer", _apiKey)
            : null;
    }

    /// <summary>规范地址：去掉尾部斜杠；只填了主机没有路径时自动补 /v1（如 http://localhost:11434）。</summary>
    private static (string, string) Normalize(string baseUrl, string model)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("模型服务地址必须是完整的 http 或 https URL。", nameof(baseUrl));
        }

        if (uri.PathAndQuery == "/")
            trimmed += "/v1";

        return (trimmed + "/", (model ?? string.Empty).Trim());
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (Model.Length == 0)
            throw new InvalidOperationException("尚未配置模型名称。");

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

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException($"无法连接模型服务（{BaseUrl}），请确认地址正确且服务可用。");
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("模型生成超时，请重试或换一个更快的模型。");
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
