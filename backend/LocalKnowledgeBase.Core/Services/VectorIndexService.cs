using System.Text.Json;

namespace LocalKnowledgeBase.Core.Services;

/// <summary>文档内一个文本块的向量。</summary>
public sealed class VectorChunk
{
    public int Index { get; set; }
    public string Text { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
}

/// <summary>一篇文档的向量记录；Vector 用于读取旧版单向量索引。</summary>
public class VectorRecord
{
    public string Model { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public List<VectorChunk> Chunks { get; set; } = new();
}

/// <summary>
/// 文档向量索引：以 JSON 文件（数据目录下 vectors.json）持久化
/// 文档 Id → embedding 向量 的映射，供混合检索（关键词 + 余弦相似度）使用。
/// 与 KnowledgeBaseService 一样采用"整文件原子写回"的持久化方式。
/// </summary>
public class VectorIndexService
{
    private readonly object _lock = new();
    private string _filePath;
    private Dictionary<string, VectorRecord> _vectors = new();

    public string DataDirectory { get; private set; }

    public static string MemoryIndexKey(string memoryId) => "memory/" + memoryId;

    /// <summary>当前已建立向量索引的条目数。</summary>
    public int Count { get { lock (_lock) return _vectors.Count; } }


    /// <summary>所有索引记录模型一致时返回该模型；混合模型或空索引返回空字符串。</summary>
    public string StoredModel
    {
        get
        {
            lock (_lock)
            {
                var models = _vectors.Values.Select(record => record.Model).Distinct(StringComparer.Ordinal).Take(2).ToList();
                return models.Count == 1 ? models[0] : string.Empty;
            }
        }
    }

    public VectorIndexService(string dataDirectory)
    {
        DataDirectory = string.Empty;
        _filePath = string.Empty;
        Reconfigure(dataDirectory);
    }

    /// <summary>切换数据目录并重新加载该目录下的索引（数据目录热切换时调用）。</summary>
    public void Reconfigure(string dataDirectory)
    {
        var fullPath = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(fullPath);
        lock (_lock)
        {
            DataDirectory = fullPath;
            _filePath = Path.Combine(fullPath, "vectors.json");
            _vectors = Load() ?? new Dictionary<string, VectorRecord>();
        }
    }

    public void Set(string documentId, string model, float[] vector)
    {
        SetChunks(documentId, model, [new VectorChunk { Vector = vector }]);
    }

    public void SetChunks(string documentId, string model, IReadOnlyList<VectorChunk> chunks)
    {
        lock (_lock)
        {
            var storedChunks = chunks.Select((chunk, index) => new VectorChunk
            {
                Index = chunk.Index == 0 && index > 0 ? index : chunk.Index,
                Text = chunk.Text,
                Vector = chunk.Vector
            }).ToList();
            _vectors[documentId] = new VectorRecord
            {
                Model = model,
                Vector = storedChunks.FirstOrDefault()?.Vector ?? Array.Empty<float>(),
                Chunks = storedChunks
            };
            Save();
        }
    }

    public IReadOnlyList<VectorChunk> GetChunks(string documentId)
    {
        lock (_lock)
            return _vectors.TryGetValue(documentId, out var record)
                ? GetChunks(record).ToList()
                : Array.Empty<VectorChunk>();
    }

    public void Remove(string documentId)
    {
        lock (_lock)
        {
            if (_vectors.Remove(documentId))
                Save();
        }
    }

    /// <summary>取指定文档的向量记录；不存在返回 null。</summary>
    public VectorRecord? Get(string documentId)
    {
        lock (_lock)
            return _vectors.TryGetValue(documentId, out var record) ? record : null;
    }

    public double GetMaxSimilarity(string documentId, float[] queryVector)
    {
        return GetMostSimilarChunk(documentId, queryVector)?.Similarity ?? 0;
    }

    public double GetMaxSimilarity(string entryId, bool isMemory, float[] queryVector) =>
        GetMaxSimilarity(isMemory ? MemoryIndexKey(entryId) : entryId, queryVector);

    public (VectorChunk Chunk, double Similarity)? GetMostSimilarChunk(string documentId, float[] queryVector)
    {
        lock (_lock)
        {
            if (!_vectors.TryGetValue(documentId, out var record))
                return null;

            var best = GetChunks(record)
                .Where(chunk => chunk.Vector.Length == queryVector.Length)
                .Select(chunk => (Chunk: chunk, Similarity: CosineSimilarity(queryVector, chunk.Vector)))
                .OrderByDescending(result => result.Similarity)
                .FirstOrDefault();
            return best.Chunk is null ? null : best;
        }
    }

    /// <summary>移除所有不在 givenIds 集合中的条目（清理孤儿向量）。</summary>
    public void RemoveExcept(IEnumerable<string> documentIds)
    {
        lock (_lock)
        {
            var keep = documentIds.ToHashSet();
            var removed = _vectors.Keys.Where(id => !keep.Contains(id)).ToList();
            if (removed.Count == 0)
                return;
            foreach (var id in removed)
                _vectors.Remove(id);
            Save();
        }
    }

    /// <summary>
    /// 找出与指定文档语义最相关的其他文档（按余弦相似度降序）。
    /// 源文档无向量、或目标向量维度不一致（换模型遗留）的条目自动排除。
    /// 返回 (文档Id, 相似度)；源文档未建向量时返回空列表。
    /// </summary>
    public IReadOnlyList<(string DocumentId, double Similarity)> FindMostSimilar(string documentId, int topK = 5)
    {
        lock (_lock)
        {
            if (!_vectors.TryGetValue(documentId, out var source))
                return Array.Empty<(string DocumentId, double Similarity)>();

            var sourceChunks = GetChunks(source);
            if (sourceChunks.Count == 0)
                return Array.Empty<(string DocumentId, double Similarity)>();

            return _vectors
                .Where(kv => kv.Key != documentId && !kv.Key.StartsWith("memory/", StringComparison.Ordinal) && kv.Value.Model == source.Model)
                .Select(kv =>
                {
                    var similarity = sourceChunks
                        .SelectMany(sourceChunk => GetChunks(kv.Value)
                            .Where(targetChunk => targetChunk.Vector.Length == sourceChunk.Vector.Length)
                            .Select(targetChunk => CosineSimilarity(sourceChunk.Vector, targetChunk.Vector)))
                        .DefaultIfEmpty(0)
                        .Max();
                    return (DocumentId: kv.Key, Similarity: similarity);
                })
                .Where(x => x.Similarity > 0)
                .OrderByDescending(x => x.Similarity)
                .Take(Math.Clamp(topK, 1, 20))
                .ToList();
        }
    }

    private static List<VectorChunk> GetChunks(VectorRecord record) =>
        record.Chunks is { Count: > 0 }
            ? record.Chunks
            : record.Vector is { Length: > 0 }
                ? [new VectorChunk { Vector = record.Vector }]
                : [];

    /// <summary>余弦相似度，范围 [-1, 1]。零向量返回 0。</summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length)
            return 0;

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA == 0 || normB == 0)
            return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private Dictionary<string, VectorRecord>? Load()
    {
        if (!File.Exists(_filePath))
            return null;
        try
        {
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonSerializer.Deserialize<Dictionary<string, VectorRecord>>(json);
        }
        catch
        {
            return null;
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_vectors, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
