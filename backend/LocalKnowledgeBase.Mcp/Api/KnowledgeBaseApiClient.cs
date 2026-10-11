using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using LocalKnowledgeBase.Core;

namespace LocalKnowledgeBase.Mcp.Api;

/// <summary>
/// 本地 Web API 的最小客户端，供 MCP 工具读写知识库。
/// 数据一律经由 API（单一写入方），避免与 API 进程的内存缓存互相覆盖。
/// </summary>
/// <remarks>
/// 失败约定：方法失败时不抛异常，返回 null / false，并把可读原因写入 <see cref="LastError"/>，
/// 由 MCP 工具转成给模型看的提示文字。
/// </remarks>
public sealed class KnowledgeBaseApiClient : IDisposable
{
    public sealed record StatusResponse(string Name, string Stage, string Mode);

    public sealed record SearchResponse(string Query, List<Document> Results);

    /// <summary>GET（POST）/api/ai/context 的返回。</summary>
    public sealed record AiSourceDto(int Index, string Title, string DocumentId);

    public sealed record RelatedDocumentDto(string Title, string DocumentId, double Similarity);

    public sealed record RelatedResponse(string DocumentId, List<RelatedDocumentDto> Related);

    public sealed record ProfileResponse(string Preset, int MaxResults, int SnippetLength, int MaxTotalChars, double TokenWeight);

    public sealed record AiContextResponse(string Query, int TotalHits, int IncludedCount, bool Truncated, string Context, List<AiSourceDto> Sources);

    private readonly HttpClient _httpClient;

    public string BaseUrl { get; }
    public string? LastError { get; private set; }

    public KnowledgeBaseApiClient(string baseUrl)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private async Task<T?> SendAsync<T>(Func<Task<T?>> request, [CallerMemberName] string? operation = null)
    {
        LastError = null;
        try
        {
            return await request();
        }
        catch (Exception ex)
        {
            LastError = ex is HttpRequestException or TaskCanceledException
                ? $"无法连接本地 API（{BaseUrl}）。请先运行 scripts/run-api.ps1 启动服务。"
                : ex.Message;
            return default;
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return default;
        return await response.Content.ReadFromJsonAsync<T>(Json.Options);
    }

    // ---- 状态 ----

    public Task<StatusResponse?> GetStatusAsync() =>
        SendAsync(async () =>
            await ReadJsonAsync<StatusResponse>(
                await _httpClient.GetAsync("api/knowledge-base/status")));

    // ---- 文档 ----

    public Task<List<Document>?> GetDocumentsAsync() =>
        SendAsync(async () =>
            await ReadJsonAsync<List<Document>>(
                await _httpClient.GetAsync("api/knowledge-base/documents")));

    public Task<Document?> GetDocumentAsync(string id) =>
        SendAsync(async () =>
            await ReadJsonAsync<Document>(
                await _httpClient.GetAsync($"api/knowledge-base/documents/{id}")));

    public Task<Document?> AddDocumentAsync(string title, string fileName, byte[] content, string? tags = null) =>
        SendAsync(async () =>
            await ReadJsonAsync<Document>(
                await _httpClient.PostAsJsonAsync("api/knowledge-base/documents",
                    new { Title = title, FileName = fileName, FileContent = content, Tags = tags }, Json.Options)));

    public Task<SearchResponse?> SearchDocumentsAsync(string query, int? max = null) =>
        SendAsync(async () =>
            await ReadJsonAsync<SearchResponse>(
                await _httpClient.GetAsync(
                    $"api/knowledge-base/search?q={Uri.EscapeDataString(query)}&max={max}")));

    /// <summary>切换检索预算档位（light / standard / deep）。</summary>
    public Task<ProfileResponse?> SetProfileAsync(string preset) =>
        SendAsync(async () =>
            await ReadJsonAsync<ProfileResponse>(
                await _httpClient.PutAsJsonAsync("api/ai/profile", new { Preset = preset }, Json.Options)));

    /// <summary>AI 接口：与指定文档语义最相关的其他文档。</summary>
    public Task<RelatedResponse?> GetRelatedAsync(string id, int max = 5) =>
        SendAsync(async () =>
            await ReadJsonAsync<RelatedResponse>(
                await _httpClient.GetAsync($"api/ai/related/{id}?max={max}")));

    /// <summary>AI 接口：检索并打包成可直接注入提示词的上下文。</summary>
    public Task<AiContextResponse?> GetAiContextAsync(string query, int? max = null, int? maxTotalChars = null) =>
        SendAsync(async () =>
            await ReadJsonAsync<AiContextResponse>(
                await _httpClient.PostAsJsonAsync("api/ai/context",
                    new { Query = query, Max = max, MaxTotalChars = maxTotalChars }, Json.Options)));

    // ---- 项目记忆 ----

    public Task<List<MemoryEntry>?> GetMemoryEntriesAsync() =>
        SendAsync(async () =>
            await ReadJsonAsync<List<MemoryEntry>>(
                await _httpClient.GetAsync("api/knowledge-base/memory")));

    public Task<MemoryEntry?> GetMemoryEntryAsync(string id) =>
        SendAsync(async () =>
            await ReadJsonAsync<MemoryEntry>(
                await _httpClient.GetAsync($"api/knowledge-base/memory/{id}")));

    public Task<List<MemoryEntry>?> SearchMemoryEntriesAsync(string query) =>
        SendAsync(async () =>
            await ReadJsonAsync<List<MemoryEntry>>(
                await _httpClient.GetAsync(
                    $"api/knowledge-base/memory/search?q={Uri.EscapeDataString(query)}")));

    public Task<MemoryEntry?> AddMemoryEntryAsync(string title, string content, string? tags) =>
        SendAsync(async () =>
            await ReadJsonAsync<MemoryEntry>(
                await _httpClient.PostAsJsonAsync("api/knowledge-base/memory",
                    new { Title = title, Content = content, Tags = tags }, Json.Options)));

    public Task<MemoryEntry?> UpdateMemoryEntryAsync(string id, string title, string content, string? tags) =>
        SendAsync(async () =>
            await ReadJsonAsync<MemoryEntry>(
                await _httpClient.PutAsJsonAsync($"api/knowledge-base/memory/{id}",
                    new { Title = title, Content = content, Tags = tags }, Json.Options)));

    public void Dispose() => _httpClient.Dispose();
}
