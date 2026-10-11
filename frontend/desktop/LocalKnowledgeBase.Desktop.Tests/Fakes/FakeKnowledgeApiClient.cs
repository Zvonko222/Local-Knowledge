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
            // 模拟检索：资料命中关键词，或提问句子中提到了资料标题（问答场景）
            Results = Documents
                .Where(d => d.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            d.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            (d.Title.Length > 0 && query.Contains(d.Title, StringComparison.OrdinalIgnoreCase)))
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

    public GetDataDirectoryResponse? NextDataDirectory { get; set; }

    public Task<GetDataDirectoryResponse?> GetDataDirectoryAsync() =>
        Task.FromResult<GetDataDirectoryResponse?>(NextDataDirectory
            ?? new GetDataDirectoryResponse { Path = null, Configured = false });

    public Task<bool> SetDataDirectoryAsync(string path) => Task.FromResult(true);

    public List<RelatedDocumentDto> RelatedToReturn { get; } = new();

    public string CurrentProfile { get; private set; } = "standard";

    public Task<RetrievalProfileDto?> GetProfileAsync() =>
        Task.FromResult<RetrievalProfileDto?>(new RetrievalProfileDto
        {
            Preset = CurrentProfile,
            MaxResults = CurrentProfile == "light" ? 3 : 6,
            SnippetLength = CurrentProfile == "light" ? 400 : 800,
            MaxTotalChars = CurrentProfile == "light" ? 1500 : 4000,
            TokenWeight = CurrentProfile == "light" ? 0.4 : 1.0
        });

    public Task<RetrievalProfileDto?> SetProfileAsync(string preset)
    {
        CurrentProfile = preset;
        return GetProfileAsync();
    }

    public Task<List<RelatedDocumentDto>?> GetRelatedAsync(string id, int max = 5) =>
        Task.FromResult<List<RelatedDocumentDto>?>(RelatedToReturn.ToList());

    public void Dispose()
    {
    }
}

/// <summary>无 UI 的 IDialogService 假实现：不弹窗，返回预设值。</summary>
public sealed class FakeDialogService : IDialogService
{
    public List<string> NextOpenFiles { get; } = new();
    public string? NextSavePath { get; set; }
    public string? NextFolder { get; set; }
    public bool NextConfirmResult { get; set; }
    public int ConfirmCallCount { get; private set; }
    public int OpenInExplorerCallCount { get; private set; }

    public IReadOnlyList<string> PickOpenFiles(string title, string filter, string? initialDirectory = null) =>
        NextOpenFiles;

    public string? PickFolder(string title) => NextFolder;

    public string? PickSaveFile(string title, string filter, string defaultFileName) => NextSavePath;

    public void OpenInExplorer(string path) => OpenInExplorerCallCount++;

    public bool Confirm(string title, string message)
    {
        ConfirmCallCount++;
        return NextConfirmResult;
    }
}

/// <summary>内存版 ILlmClient：不访问网络，返回预设回答并记录收到的提示词。</summary>
public sealed class FakeLlmClient : ILlmClient
{
    public string BaseUrl { get; private set; } = "http://localhost:11434/v1";
    public string Model { get; private set; } = string.Empty;
    public string ApiKey { get; private set; } = string.Empty;
    public bool Configured { get; private set; }
    public int CompleteCallCount { get; private set; }
    public string? LastSystemPrompt { get; private set; }
    public string? LastUserPrompt { get; private set; }
    public string NextAnswer { get; set; } = "（测试回答）依据资料 [1] 得出结论。";
    public bool TestResult { get; set; }
    /// <summary>为 true 时 CompleteAsync 抛异常，用于测试失败路径。</summary>
    public bool ThrowOnComplete { get; set; }

    public void Configure(string baseUrl, string model, string apiKey = "")
    {
        BaseUrl = baseUrl.TrimEnd('/') + "/";
        Model = model;
        ApiKey = apiKey;
        Configured = true;
    }

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (ThrowOnComplete)
            throw new InvalidOperationException("模拟的模型连接失败。");

        CompleteCallCount++;
        LastSystemPrompt = systemPrompt;
        LastUserPrompt = userPrompt;
        return Task.FromResult(NextAnswer);
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TestResult);
}
