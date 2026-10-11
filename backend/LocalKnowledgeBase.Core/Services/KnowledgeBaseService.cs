using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalKnowledgeBase.Core.Services;

/// <summary>
/// Manages the knowledge base data directory and JSON-based storage.
/// Stores documents and memory entries as JSON files with full-text search support.
/// </summary>
public class KnowledgeBaseService : IDisposable
{
    private string _dataDirectory;
    private string _documentsFile;
    private string _memoryFile;
    private readonly object _lock = new();
    private List<Document>? _documents;
    private List<MemoryEntry>? _memoryEntries;
    private bool _disposed;

    public string DataDirectory => _dataDirectory;

    public KnowledgeBaseService(string dataDirectory)
    {
        _dataDirectory = string.Empty;
        _documentsFile = string.Empty;
        _memoryFile = string.Empty;
        Reconfigure(dataDirectory);
    }

    public void Reconfigure(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("Data directory is required.", nameof(dataDirectory));

        var fullPath = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(fullPath);

        lock (_lock)
        {
            _dataDirectory = fullPath;
            _documentsFile = Path.Combine(fullPath, "documents.json");
            _memoryFile = Path.Combine(fullPath, "memory.json");
            LoadData();
        }
    }

    private void LoadData()
    {
        _documents = LoadFromFile<List<Document>>(_documentsFile) ?? new List<Document>();
        _memoryEntries = LoadFromFile<List<MemoryEntry>>(_memoryFile) ?? new List<MemoryEntry>();
    }

    private T? LoadFromFile<T>(string filePath)
    {
        if (!File.Exists(filePath))
            return default;

        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    private void SaveToFile<T>(string filePath, T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private void SaveDocuments()
    {
        SaveToFile(_documentsFile, _documents);
    }

    private void SaveMemoryEntries()
    {
        SaveToFile(_memoryFile, _memoryEntries);
    }

    #region Data directory management

    public static string? GetUserDataDirectory()
    {
        var environmentPath = Environment.GetEnvironmentVariable("LOCAL_KNOWLEDGE_BASE_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            var fullPath = Path.GetFullPath(environmentPath);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        var configPath = GetConfigPath();
        if (File.Exists(configPath))
        {
            var path = File.ReadAllText(configPath).Trim();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }
        return null;
    }

    public static void SetUserDataDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Data directory is required.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(fullPath);

        var environmentPath = Environment.GetEnvironmentVariable("LOCAL_KNOWLEDGE_BASE_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            var environmentFullPath = Path.GetFullPath(environmentPath);
            if (!string.Equals(fullPath, environmentFullPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Data directory is controlled by LOCAL_KNOWLEDGE_BASE_DATA_DIR.");
            return;
        }

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LocalKnowledgeBase");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "data_directory.txt");
        File.WriteAllText(configPath, fullPath);
    }

    private static string GetConfigPath()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LocalKnowledgeBase");
        return Path.Combine(configDir, "data_directory.txt");
    }

    #endregion

    #region Document operations

    public Document AddDocument(Document document)
    {
        lock (_lock)
        {
            _documents!.Add(document);
            SaveDocuments();
            return document;
        }
    }

    public Document? GetDocument(string id)
    {
        lock (_lock)
        {
            return _documents!.FirstOrDefault(d => d.Id == id);
        }
    }

    public List<Document> ListDocuments()
    {
        lock (_lock)
        {
            return new List<Document>(_documents!);
        }
    }

    public bool DeleteDocument(string id)
    {
        lock (_lock)
        {
            var index = _documents!.FindIndex(d => d.Id == id);
            if (index < 0)
                return false;

            var document = _documents[index];

            var rawFilePath = GetRawFilePath(document.StoredFileName);
            if (rawFilePath == null)
            {
                var legacyFileName = !string.IsNullOrWhiteSpace(document.OriginalFileName)
                    ? document.OriginalFileName
                    : document.Title + (document.FileType ?? string.Empty);
                rawFilePath = GetRawFilePath(legacyFileName);
            }

            if (rawFilePath != null && File.Exists(rawFilePath))
                File.Delete(rawFilePath);

            _documents.RemoveAt(index);
            SaveDocuments();
            return true;
        }
    }

    public Document? UpdateDocument(string id, string title, string content, string? tags)
    {
        lock (_lock)
        {
            var document = _documents!.FirstOrDefault(d => d.Id == id);
            if (document == null)
                return null;

            document.Title = title.Trim();
            document.Content = content;
            document.Tags = NormalizeTags(tags);
            document.UpdatedAt = DateTimeOffset.UtcNow;

            var rawFilePath = GetRawFilePath(document.StoredFileName);
            var extension = Path.GetExtension(document.StoredFileName)?.ToLowerInvariant();
            if (rawFilePath != null && File.Exists(rawFilePath) && extension is ".txt" or ".md")
            {
                File.WriteAllText(rawFilePath, content, new UTF8Encoding(false));
                document.FileSize = Encoding.UTF8.GetByteCount(content);
            }

            SaveDocuments();
            return document;
        }
    }

    private string? GetRawFilePath(string? storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
            return null;

        var rawDir = Path.GetFullPath(Path.Combine(_dataDirectory, "raw_files"));
        var fileName = Path.GetFileName(storedFileName);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return Path.Combine(rawDir, fileName);
    }

    #endregion

    #region Search operations

    // 检索分词用的分隔符：空白 + 中英文常见标点（用户常直接粘贴整句提问）
    private static readonly char[] QuerySeparators =
        [' ', '\t', '\r', '\n', '，', '。', '？', '！', '；', '、', '：', '（', '）', '《', '》', '“', '”',
         ',', '.', '?', '!', ';', ':', '(', ')', '<', '>', '"', '\''];

    /// <summary>
    /// 把查询拆成关键词：按空白和中英文标点切分、去重。
    /// 这样整句提问（如“项目采用了什么架构？数据保存在哪里？”）也能命中。
    /// </summary>
    public static List<string> TokenizeQuery(string query) =>
        (query ?? string.Empty)
            .Split(QuerySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>单条资料的相关度得分：
    /// 每个关键词——标题命中 ×8、标签 ×4、正文按出现次数 ×2（单词最多计 5 次）；
    /// 关键词不少于 2 个且全部命中时额外 +8（覆盖加成）。</summary>
    public static int ScoreText(string title, string? tags, string content, List<string> terms)
    {
        if (terms.Count == 0)
            return 0;

        var score = 0;
        var matchedTerms = 0;
        foreach (var term in terms)
        {
            var termMatched = false;
            if (title.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 8;
                termMatched = true;
            }
            if (tags?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
            {
                score += 4;
                termMatched = true;
            }
            var occurrences = CountOccurrences(content, term);
            if ( occurrences > 0)
            {
                score += 2 * Math.Min(occurrences, 5);
                termMatched = true;
            }
            if (termMatched)
                matchedTerms++;
        }

        if (terms.Count >= 2 && matchedTerms == terms.Count)
            score += 8; // 多关键词全覆盖加成

        return score;
    }

    /// <summary>统计词在文本中的出现次数（不区分大小写）。</summary>
    public static int CountOccurrences(string text, string term)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
            return 0;

        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += term.Length;
        }
        return count;
    }

    /// <summary>
    /// 智能摘要：优先截取包含关键词的窗口（关键词出现在窗口前 1/4 处），
    /// 无命中时退化为从头截取。结果不超过 maxLength + 省略号。
    /// </summary>
    public static string BestSnippet(string text, IReadOnlyList<string> terms, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        var firstHit = -1;
        foreach (var term in terms)
        {
            var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (firstHit < 0 || index < firstHit))
                firstHit = index;
        }

        var start = firstHit < 0 ? 0 : Math.Max(0, firstHit - maxLength / 4);
        if (start > text.Length - maxLength)
            start = Math.Max(0, text.Length - maxLength);

        var slice = text.Substring(start, Math.Min(maxLength, text.Length - start));
        var prefix = start > 0 ? "…" : string.Empty;
        var suffix = start + slice.Length < text.Length ? "…" : string.Empty;
        return prefix + slice + suffix;
    }

    /// <summary>语义命中换算成分数的系数：余弦相似度 ∈ [-1,1]，×10 后与关键词分同量纲。</summary>
    public const double SemanticWeight = 10.0;

    /// <summary>
    /// 全文检索文档（混合检索）：
    /// - 关键词分：按分词打分（标题 ×5 / 标签 ×3 / 正文 ×1）；
    /// - 语义分：传入 queryVector 时，对每篇文档计算余弦相似度 × <see cref="SemanticWeight"/>；
    /// - 总分 = 关键词分 + 语义分，按总分排序（同分按更新时间），总分 > 0 才返回。
    /// queryVector 为 null 时退化为纯关键词检索。
    /// </summary>
    public List<Document> SearchDocuments(string query, int maxResults = 20,
        VectorIndexService? vectorIndex = null, float[]? queryVector = null)
    {
        lock (_lock)
        {
            var terms = TokenizeQuery(query);
            var useSemantic = queryVector is { Length: > 0 } && vectorIndex is not null;
            if (terms.Count == 0 && !useSemantic)
                return new List<Document>();

            return _documents!
                .Select(d =>
                {
                    var score = (double)ScoreText(d.Title, d.Tags, d.Content, terms);
                    if (useSemantic)
                    {
                        var record = vectorIndex!.Get(d.Id);
                        if (record is { } rec && rec.Vector.Length == queryVector!.Length)
                        {
                            var cosine = VectorIndexService.CosineSimilarity(queryVector, rec.Vector);
                            if (cosine > 0)
                                score += cosine * SemanticWeight;
                        }
                    }
                    return (Doc: d, Score: score);
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Doc.UpdatedAt)
                .Take(Math.Clamp(maxResults, 1, 100))
                .Select(x => x.Doc)
                .ToList();
        }
    }

    #endregion

    #region Memory operations

    public MemoryEntry AddMemoryEntry(MemoryEntry entry)
    {
        lock (_lock)
        {
            _memoryEntries!.Add(entry);
            SaveMemoryEntries();
            return entry;
        }
    }

    public MemoryEntry? GetMemoryEntry(string id)
    {
        lock (_lock)
        {
            return _memoryEntries!.FirstOrDefault(m => m.Id == id);
        }
    }

    public List<MemoryEntry> ListMemoryEntries()
    {
        lock (_lock)
        {
            return new List<MemoryEntry>(_memoryEntries!);
        }
    }

    public bool DeleteMemoryEntry(string id)
    {
        lock (_lock)
        {
            var index = _memoryEntries!.FindIndex(m => m.Id == id);
            if (index < 0)
                return false;

            _memoryEntries.RemoveAt(index);
            SaveMemoryEntries();
            return true;
        }
    }

    public MemoryEntry? UpdateMemoryEntry(string id, string title, string content, string? tags)
    {
        lock (_lock)
        {
            var entry = _memoryEntries!.FirstOrDefault(m => m.Id == id);
            if (entry == null)
                return null;

            entry.Title = title.Trim();
            entry.Content = content;
            entry.Tags = NormalizeTags(tags);
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            SaveMemoryEntries();
            return entry;
        }
    }

    /// <summary>检索项目记忆：与文档检索同样的关键词打分排序。</summary>
    public List<MemoryEntry> SearchMemoryEntries(string query)
    {
        lock (_lock)
        {
            var terms = TokenizeQuery(query);
            if (terms.Count == 0)
                return new List<MemoryEntry>();

            return _memoryEntries!
                .Select(m => (Entry: m, Score: ScoreText(m.Title, m.Tags, m.Content, terms)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Entry.UpdatedAt)
                .Select(x => x.Entry)
                .ToList();
        }
    }

    #endregion

    #region Text chunking

    public static List<string> ChunkText(string text, int chunkSize = 500, int overlap = 50)
    {
        var chunks = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        for (int i = 0; i < text.Length; i += chunkSize - overlap)
        {
            var remaining = text.Length - i;
            if (remaining <= 0)
                break;

            var chunkLength = Math.Min(chunkSize, remaining);
            var chunk = text.Substring(i, chunkLength);
            chunks.Add(chunk);

            if (remaining <= chunkSize)
                break;
        }

        return chunks;
    }

    public static string ExtractTextFromFile(string filePath, byte[]? fileContent = null)
    {
        if (fileContent != null)
        {
            using var stream = new MemoryStream(fileContent);
            return ExtractTextFromStream(stream, filePath);
        }

        return File.ReadAllText(filePath);
    }

    private static string ExtractTextFromStream(Stream stream, string filePath)
    {
        // 提取逻辑统一在 TextExtractor：pdf/docx 专用解析，其余按文本读取
        return TextExtractor.Extract(stream, filePath);
    }

    private static string ReadAllText(Stream stream)
    {
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }

    #endregion

    /// <summary>公开的标签规范化（去空白、去重、逗号分隔），供 API 导入时使用。</summary>
    public static string? NormalizeTagsPublic(string? tags) => NormalizeTags(tags);

    private static string? NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return null;

        return string.Join(", ", tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
        }
    }
}
