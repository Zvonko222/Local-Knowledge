using System.IO;
using LocalKnowledgeBase.Desktop.Models;
using LocalKnowledgeBase.Desktop.Services;

namespace LocalKnowledgeBase.Desktop.Tests.Fakes;

/// <summary>
/// 内存版 IKnowledgeApiClient：数据存在本地列表里，供 ViewModel 单测使用。
/// </summary>
public sealed class FakeKnowledgeApiClient : IKnowledgeApiClient
{
    public List<Document> Documents { get; } = new();
    public List<MemoryEntry> MemoryEntries { get; } = new();

    public string BaseUrl { get; private set; } = "https://localhost:7047";
    public string? LastError { get; private set; }
    public int AddDocumentCallCount { get; private set; }
    public int AddMemoryCallCount { get; private set; }

    public void SetBaseUrl(string baseUrl) => BaseUrl = baseUrl.TrimEnd('/');

    public Task<KnowledgeBaseStatus?> GetStatusAsync() =>
        Task.FromResult<KnowledgeBaseStatus?>(new KnowledgeBaseStatus());

    public Task<List<Document>?> GetDocumentsAsync() =>
        Task.FromResult<List<Document>?>(Documents.ToList());

    public Task<Document?> GetDocumentAsync(string id) =>
        Task.FromResult(Documents.FirstOrDefault(d => d.Id == id));

    public Task<Document?> AddDocumentAsync(string title, string fileName, byte[] content)
    {
        AddDocumentCallCount++;
        var document = new Document
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            OriginalFileName = fileName,
            FileSize = content.LongLength,
            FileType = Path.GetExtension(fileName).TrimStart('.'),
            Content = string.Empty,
            Tags = null,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };
        Documents.Add(document);
        return Task.FromResult<Document?>(document);
    }

    public Task<Document?> UpdateDocumentAsync(string id, string title, string content, string? tags)
    {
        var document = Documents.FirstOrDefault(d => d.Id == id);
        if (document is null)
        {
            LastError = "文档不存在";
            return Task.FromResult<Document?>(null);
        }

        document.Title = title;
        document.Content = content;
        document.Tags = tags;
        document.UpdatedAt = DateTimeOffset.Now;
        return Task.FromResult<Document?>(document);
    }

    public Task<bool> DeleteDocumentAsync(string id)
    {
        var count = Documents.RemoveAll(d => d.Id == id);
        return Task.FromResult(count > 0);
    }

    public Task<SearchResults?> SearchAsync(string query) =>
        Task.FromResult<SearchResults?>(new SearchResults
        {
            Query = query,
            Results = Documents
                .Where(d => d.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList()
        });

    public Task<List<MemoryEntry>?> GetMemoryEntriesAsync() =>
        Task.FromResult<List<MemoryEntry>?>(MemoryEntries.ToList());

    public Task<MemoryEntry?> AddMemoryEntryAsync(string title, string content, string? tags = null)
    {
        AddMemoryCallCount++;
        var entry = new MemoryEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            Content = content,
            Tags = tags,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };
        MemoryEntries.Add(entry);
        return Task.FromResult<MemoryEntry?>(entry);
    }

    public Task<MemoryEntry?> UpdateMemoryEntryAsync(string id, string title, string content, string? tags = null)
    {
        var entry = MemoryEntries.FirstOrDefault(m => m.Id == id);
        if (entry is null)
        {
            LastError = "记忆不存在";
            return Task.FromResult<MemoryEntry?>(null);
        }

        entry.Title = title;
        entry.Content = content;
        entry.Tags = tags;
        entry.UpdatedAt = DateTimeOffset.Now;
        return Task.FromResult<MemoryEntry?>(entry);
    }

    public Task<bool> DeleteMemoryEntryAsync(string id)
    {
        var count = MemoryEntries.RemoveAll(m => m.Id == id);
        return Task.FromResult(count > 0);
    }

    public Task<List<MemoryEntry>?> SearchMemoryAsync(string query) =>
        Task.FromResult<List<MemoryEntry>?>(MemoryEntries
            .Where(m => m.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList());

    public Task<GetDataDirectoryResponse?> GetDataDirectoryAsync() =>
        Task.FromResult<GetDataDirectoryResponse?>(new GetDataDirectoryResponse { Path = null, Configured = false });

    public Task<bool> SetDataDirectoryAsync(string path) => Task.FromResult(true);

    public void Dispose()
    {
    }
}

/// <summary>无 UI 的 IDialogService 假实现：不弹窗，返回预设值。</summary>
public sealed class FakeDialogService : IDialogService
{
    public string? NextOpenFile { get; set; }
    public bool NextConfirmResult { get; set; }
    public int ConfirmCallCount { get; private set; }

    public string? PickOpenFile(string title, string filter, string? initialDirectory = null) => NextOpenFile;

    public string? PickFolder(string title) => null;

    public bool Confirm(string title, string message)
    {
        ConfirmCallCount++;
        return NextConfirmResult;
    }
}
