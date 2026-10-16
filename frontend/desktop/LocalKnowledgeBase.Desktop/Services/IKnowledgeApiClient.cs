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

    /// <summary>与指定文档语义最相关的其他文档（基于后端向量索引）。</summary>
    Task<List<RelatedDocumentDto>?> GetRelatedAsync(string id, int max = 5);

    /// <summary>文档原始文件的完整路径；无原始文件时返回 null。</summary>
    Task<string?> GetDocumentFilePathAsync(string id);

    /// <summary>读取当前检索预算档位。</summary>
    Task<RetrievalProfileDto?> GetProfileAsync();

    /// <summary>切换检索预算档位（light / standard / deep），对所有检索入口生效。</summary>
    Task<RetrievalProfileDto?> SetProfileAsync(string preset);
}

/// <summary>检索预算档位信息。</summary>
public class RetrievalProfileDto
{
    public string Preset { get; set; } = string.Empty;
    public int MaxResults { get; set; }
    public int SnippetLength { get; set; }
    public int MaxTotalChars { get; set; }
    public double TokenWeight { get; set; }
}

/// <summary>相关文档条目：标题、编号与相似度（0~1）。</summary>
public class RelatedDocumentDto
{
    public string Title { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public double Similarity { get; set; }
}
