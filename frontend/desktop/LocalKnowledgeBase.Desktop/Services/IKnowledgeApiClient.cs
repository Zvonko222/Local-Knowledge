using LocalKnowledgeBase.Desktop.Models;

namespace LocalKnowledgeBase.Desktop.Services;

/// <summary>
/// 后端 Web API 的客户端抽象。ViewModel 只依赖此接口，
/// 便于单元测试时替换为内存假实现（见 LocalKnowledgeBase.Desktop.Tests/Fakes）。
/// </summary>
/// <remarks>
/// 失败约定：所有方法失败时不抛异常，返回 null / false，并把可读的
/// 错误描述写入 <see cref="LastError"/>，由 ViewModel 展示给用户。
/// </remarks>
public interface IKnowledgeApiClient : IDisposable
{
    /// <summary>当前 API 基地址（不含尾部斜杠）。</summary>
    string BaseUrl { get; }

    /// <summary>最近一次调用失败的原因；成功调用后会被清空。</summary>
    string? LastError { get; }

    /// <summary>切换 API 地址。地址非法时抛 <see cref="ArgumentException"/>。</summary>
    void SetBaseUrl(string baseUrl);

    Task<KnowledgeBaseStatus?> GetStatusAsync();
    Task<List<Document>?> GetDocumentsAsync();
    Task<Document?> GetDocumentAsync(string id);
    Task<Document?> AddDocumentAsync(string title, string fileName, byte[] content);
    Task<Document?> UpdateDocumentAsync(string id, string title, string content, string? tags);
    Task<bool> DeleteDocumentAsync(string id);
    Task<SearchResults?> SearchAsync(string query);

    Task<List<MemoryEntry>?> GetMemoryEntriesAsync();
    Task<MemoryEntry?> AddMemoryEntryAsync(string title, string content, string? tags = null);
    Task<MemoryEntry?> UpdateMemoryEntryAsync(string id, string title, string content, string? tags = null);
    Task<bool> DeleteMemoryEntryAsync(string id);
    Task<List<MemoryEntry>?> SearchMemoryAsync(string query);

    Task<GetDataDirectoryResponse?> GetDataDirectoryAsync();
    Task<bool> SetDataDirectoryAsync(string path);
}
