using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using LocalKnowledgeBase.Desktop.Models;

namespace LocalKnowledgeBase.Desktop.Services;

/// <summary>
/// <see cref="IKnowledgeApiClient"/> 的 HTTP 实现，通过本地 Web API 读写知识库。
/// </summary>
public sealed class KnowledgeBaseApiClient : IKnowledgeApiClient
{
    // 本地 API 使用 ASP.NET Core 开发证书（https://localhost:7047）。
    // 若开发证书未信任，可执行 `dotnet dev-certs https --trust`；
    // 这里保留对本地开发证书的放行，避免未信任证书导致桌面端无法连接。
    private HttpClient _httpClient;

    public string BaseUrl => _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;

    public string? LastError { get; private set; }

    public KnowledgeBaseApiClient(string baseUrl = ClientSettings.DefaultApiBaseUrl)
    {
        _httpClient = CreateHttpClient(baseUrl);
    }

    public void SetBaseUrl(string baseUrl)
    {
        var nextClient = CreateHttpClient(baseUrl);
        var previousClient = _httpClient;
        _httpClient = nextClient;
        previousClient.Dispose();
    }

    private static HttpClient CreateHttpClient(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("API 地址必须是完整的 http 或 https URL。", nameof(baseUrl));
        }

        return new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            BaseAddress = new Uri(baseUri.ToString().TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    // ---- 请求助手：统一“失败不抛异常，写入 LastError”的约定 ----

    private async Task<T?> SendAsync<T>(Func<Task<T?>> request, [CallerMemberName] string? operation = null)
    {
        LastError = null;
        try
        {
            return await request();
        }
        catch (Exception ex)
        {
            LastError = DescribeError(ex, operation);
            return default;
        }
    }

    private async Task<bool> SendFlagAsync(Func<Task<bool>> request, [CallerMemberName] string? operation = null)
    {
        LastError = null;
        try
        {
            return await request();
        }
        catch (Exception ex)
        {
            LastError = DescribeError(ex, operation);
            return false;
        }
    }

    private static bool IsSuccess(HttpResponseMessage response) => response.IsSuccessStatusCode;

    private static string DescribeError(Exception ex, string? operation)
    {
        var message = ex switch
        {
            HttpRequestException => "无法连接 API 服务，请确认服务已启动。",
            TaskCanceledException => "API 请求超时，请确认服务已启动。",
            _ => ex.Message
        };
        Debug.WriteLine($"[ApiClient] {operation} 失败: {ex}");
        return message;
    }

    // ---- 状态与数据目录 ----

    public Task<KnowledgeBaseStatus?> GetStatusAsync() =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync("api/knowledge-base/status");
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<KnowledgeBaseStatus>()
                : null;
        });

    public Task<GetDataDirectoryResponse?> GetDataDirectoryAsync() =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync("api/knowledge-base/data-directory");
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<GetDataDirectoryResponse>()
                : null;
        });

    public Task<bool> SetDataDirectoryAsync(string path) =>
        SendFlagAsync(async () =>
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/knowledge-base/data-directory", new SetDataDirectoryRequest { Path = path });
            return IsSuccess(response);
        });

    public Task<RetrievalProfileDto?> GetProfileAsync() =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync("api/ai/profile");
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<RetrievalProfileDto>()
                : null;
        });

    public Task<RetrievalProfileDto?> SetProfileAsync(string preset) =>
        SendAsync(async () =>
        {
            var response = await _httpClient.PutAsJsonAsync("api/ai/profile", new { Preset = preset });
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<RetrievalProfileDto>()
                : null;
        });

    public Task<string?> GetDocumentFilePathAsync(string id) =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync($"api/knowledge-base/documents/{id}/file-path");
            if (!IsSuccess(response))
                return null;
            using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.TryGetProperty("path", out var path)
                ? path.GetString()
                : null;
        });

    public Task<List<RelatedDocumentDto>?> GetRelatedAsync(string id, int max = 5) =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync($"api/ai/related/{id}?max={max}");
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<List<RelatedDocumentDto>>()
                : null;
        });

    // ---- 文档 ----

    public Task<List<Document>?> GetDocumentsAsync() =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync("api/knowledge-base/documents");
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<List<Document>>()
                : null;
        });

    public Task<Document?> GetDocumentAsync(string id) =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync($"api/knowledge-base/documents/{id}");
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<Document>()
                : null;
        });

    public Task<Document?> AddDocumentAsync(string title, string fileName, byte[] content) =>
        SendAsync(async () =>
        {
            var request = new { Title = title, FileName = fileName, FileContent = content };
            var response = await _httpClient.PostAsJsonAsync("api/knowledge-base/documents", request);
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<Document>()
                : null;
        });

    public Task<Document?> UpdateDocumentAsync(string id, string title, string content, string? tags) =>
        SendAsync(async () =>
        {
            var request = new { Title = title, Content = content, Tags = tags };
            var response = await _httpClient.PutAsJsonAsync($"api/knowledge-base/documents/{id}", request);
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<Document>()
                : null;
        });

    public Task<bool> DeleteDocumentAsync(string id) =>
        SendFlagAsync(async () =>
            IsSuccess(await _httpClient.DeleteAsync($"api/knowledge-base/documents/{id}")));

    public Task<SearchResults?> SearchAsync(string query) =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync(
                $"api/knowledge-base/search?q={Uri.EscapeDataString(query)}");
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<SearchResults>()
                : null;
        });

    // ---- 项目记忆 ----

    public Task<List<MemoryEntry>?> GetMemoryEntriesAsync() =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync("api/knowledge-base/memory");
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<List<MemoryEntry>>()
                : null;
        });

    public Task<MemoryEntry?> AddMemoryEntryAsync(string title, string content, string? tags = null) =>
        SendAsync(async () =>
        {
            var request = new { Title = title, Content = content, Tags = tags };
            var response = await _httpClient.PostAsJsonAsync("api/knowledge-base/memory", request);
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<MemoryEntry>()
                : null;
        });

    public Task<MemoryEntry?> UpdateMemoryEntryAsync(string id, string title, string content, string? tags = null) =>
        SendAsync(async () =>
        {
            var request = new { Title = title, Content = content, Tags = tags };
            var response = await _httpClient.PutAsJsonAsync($"api/knowledge-base/memory/{id}", request);
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<MemoryEntry>()
                : null;
        });

    public Task<bool> DeleteMemoryEntryAsync(string id) =>
        SendFlagAsync(async () =>
            IsSuccess(await _httpClient.DeleteAsync($"api/knowledge-base/memory/{id}")));

    public Task<List<MemoryEntry>?> SearchMemoryAsync(string query) =>
        SendAsync(async () =>
        {
            var response = await _httpClient.GetAsync(
                $"api/knowledge-base/memory/search?q={Uri.EscapeDataString(query)}");
            return IsSuccess(response)
                ? await response.Content.ReadFromJsonAsync<List<MemoryEntry>>()
                : null;
        });

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
