using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalKnowledgeBase.Core.Services;

/// <summary>
/// OpenAI 兼容 embedding 客户端（POST {base}/embeddings）。
/// 智谱（embedding-3）、OpenAI（text-embedding-3-small）、Ollama（nomic-embed-text）等
/// 均使用该协议。检索时把查询与文档都转成向量，再做余弦相似度混合排序。
/// </summary>
public sealed class EmbeddingClient
{
    private readonly HttpClient _httpClient;
    private string _apiKey = string.Empty;

    public string BaseUrl { get; private set; }
    public string Model { get; private set; }
    public string ApiKey => _apiKey;

    public EmbeddingClient(string baseUrl, string model, string apiKey = "", HttpMessageHandler? handler = null)
    {
        (BaseUrl, Model) = Normalize(baseUrl, model);
        _apiKey = (apiKey ?? string.Empty).Trim();
        _httpClient = handler is null
            ? new HttpClient(new HttpClientHandler { UseProxy = false })
            { Timeout = TimeSpan.FromMinutes(2) }
            : new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
        // 本地知识库只访问用户配置的服务地址（多为 localhost），
        // 显式禁用系统代理，避免 Clash 类工具拦截导致请求挂起
        _httpClient.BaseAddress = new Uri(BaseUrl);
        // 每次请求使用独立连接：本地/内网调用开销可忽略，且避免对端 keep-alive 实现差异
        // （如轻量 stub/代理）导致的请求体丢失问题
        _httpClient.DefaultRequestHeaders.ConnectionClose = true;
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
        _httpClient.DefaultRequestHeaders.Authorization = _apiKey.Length > 0
            ? new AuthenticationHeaderValue("Bearer", _apiKey)
            : null;
    }

    private static (string, string) Normalize(string baseUrl, string model)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Embedding 服务地址必须是完整的 http 或 https URL。", nameof(baseUrl));
        }
        if (uri.PathAndQuery == "/")
            trimmed += "/v1";
        return (trimmed + "/", (model ?? string.Empty).Trim());
    }

    /// <summary>批量生成向量；失败返回 null（调用方应退化为纯关键词检索），并把原因写入 LastError。</summary>
    public async Task<List<float[]>?> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (Model.Length == 0 || texts.Count == 0)
            return null;

        try
        {
            // 显式序列化 + ByteArrayContent：保证请求恒带 Content-Length（部分本地服务不认 chunked）
            var body = JsonSerializer.SerializeToUtf8Bytes(
                new { model = Model, input = texts });
            using var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await _httpClient.PostAsync("embeddings", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var data = document.RootElement.GetProperty("data");

            var indexedVectors = new List<(int Index, float[] Vector)>();
            foreach (var item in data.EnumerateArray())
            {
                var vector = new List<float>();
                foreach (var value in item.GetProperty("embedding").EnumerateArray())
                    vector.Add(value.GetSingle());
                var index = item.TryGetProperty("index", out var indexValue)
                    ? indexValue.GetInt32()
                    : indexedVectors.Count;
                indexedVectors.Add((index, vector.ToArray()));
            }
            return indexedVectors.OrderBy(item => item.Index).Select(item => item.Vector).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public async Task<List<float[]>?> EmbedBatchedAsync(IReadOnlyList<string> texts, int batchSize = 32,
        CancellationToken cancellationToken = default)
    {
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (texts.Count == 0)
            return new List<float[]>();

        var result = new List<float[]>(texts.Count);
        for (var offset = 0; offset < texts.Count; offset += batchSize)
        {
            var batch = texts.Skip(offset).Take(batchSize).ToList();
            var vectors = await EmbedBatchAsync(batch, cancellationToken);
            if (vectors is null || vectors.Count != batch.Count || vectors.Any(vector => vector.Length == 0))
                return null;
            result.AddRange(vectors);
        }

        return result;
    }

    public async Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var vectors = await EmbedBatchAsync(new[] { text }, cancellationToken);
        return vectors is { Count: > 0 } ? vectors[0] : null;
    }

    public string? LastError { get; private set; }
}
