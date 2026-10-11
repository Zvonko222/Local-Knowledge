using System.Text.Json;

namespace LocalKnowledgeBase.Core.Services;

/// <summary>一条文档的向量记录。</summary>
public class VectorRecord
{
    /// <summary>生成该向量所用的 embedding 模型名（换模型后需重建索引）。</summary>
    public string Model { get; set; } = string.Empty;

    public float[] Vector { get; set; } = Array.Empty<float>();
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

    /// <summary>当前索引中的向量数量。</summary>
    public int Count { get { lock (_lock) return _vectors.Count; } }

    /// <summary>索引使用的 embedding 模型名（取任意一条记录；空表示索引为空）。</summary>
    public string StoredModel
    {
        get
        {
            lock (_lock)
                return _vectors.Values.FirstOrDefault()?.Model ?? string.Empty;
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
        lock (_lock)
        {
            _vectors[documentId] = new VectorRecord { Model = model, Vector = vector };
            Save();
        }
    }

    public void Remove(string documentId)
    {
        lock (_lock)
        {
            if (_vectors.Remove(documentId))
                Save();
        }
    }

    /// <summary>取指定文档的向量；不存在返回 null。</summary>
    public VectorRecord? Get(string documentId)
    {
        lock (_lock)
            return _vectors.TryGetValue(documentId, out var record) ? record : null;
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
            if (!_vectors.TryGetValue(documentId, out var source) || source.Vector.Length == 0)
                return Array.Empty<(string DocumentId, double Similarity)>();

            return _vectors
                .Where(kv => kv.Key != documentId && kv.Value.Vector.Length == source.Vector.Length)
                .Select(kv => (DocumentId: kv.Key, Similarity: CosineSimilarity(source.Vector, kv.Value.Vector)))
                .Where(x => x.Similarity > 0)
                .OrderByDescending(x => x.Similarity)
                .Take(Math.Clamp(topK, 1, 20))
                .ToList();
        }
    }

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
