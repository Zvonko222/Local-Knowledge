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

    public List<Document> SearchDocuments(string query, int maxResults = 20)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Document>();

            var lowerQuery = query.ToLowerInvariant();
            return _documents!
                .Where(d => d.Title.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                           d.Content.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                           (d.Tags?.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(Math.Clamp(maxResults, 1, 100))
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

    public List<MemoryEntry> SearchMemoryEntries(string query)
    {
        lock (_lock)
        {
            var lowerQuery = query.ToLowerInvariant();
            var lowerQueryTrimmed = lowerQuery.Trim();

            return _memoryEntries!
                .Where(m => m.Title.Contains(lowerQueryTrimmed, StringComparison.OrdinalIgnoreCase) ||
                           m.Content.Contains(lowerQueryTrimmed, StringComparison.OrdinalIgnoreCase) ||
                           (m.Tags?.Contains(lowerQueryTrimmed, StringComparison.OrdinalIgnoreCase) ?? false))
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
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();

        return ext switch
        {
            ".txt" => ReadAllText(stream),
            ".md" => ReadAllText(stream),
            _ => ReadAllText(stream) // Default: read as text
        };
    }

    private static string ReadAllText(Stream stream)
    {
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }

    #endregion

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
