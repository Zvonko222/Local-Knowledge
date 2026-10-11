namespace LocalKnowledgeBase.Desktop.Models;

/// <summary>
/// 与后端 API 的 JSON 契约模型。
/// 属性名与 <c>backend/LocalKnowledgeBase.Api/Controllers/KnowledgeBaseController.cs</c>
/// 返回的 JSON 字段保持一致，改字段名必须先确认后端契约。
/// </summary>

/// <summary>GET /status 的返回。注意：后端把数据目录填充在 Mode 字段中（历史命名）。</summary>
public class KnowledgeBaseStatus
{
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    /// <summary>后端实际填充的是当前数据目录路径。</summary>
    public string Mode { get; set; } = string.Empty;
}

/// <summary>知识库中的一份文档。</summary>
public class Document
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? FileType { get; set; }
    public string? OriginalFileName { get; set; }
    public long FileSize { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>一条项目记忆（决策、偏好等长期上下文）。</summary>
public class MemoryEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>POST /data-directory 的请求体。</summary>
public class SetDataDirectoryRequest
{
    public string Path { get; set; } = string.Empty;
}

/// <summary>GET /data-directory 的返回。</summary>
public class GetDataDirectoryResponse
{
    public string? Path { get; set; }
    public bool Configured { get; set; }
}

/// <summary>GET /search 的返回。</summary>
public class SearchResults
{
    public string Query { get; set; } = string.Empty;
    public List<Document> Results { get; set; } = new();
}
