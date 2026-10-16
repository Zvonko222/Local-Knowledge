using System.Threading.Channels;
using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;

namespace LocalKnowledgeBase.Api.Services;

/// <summary>
/// 后台向量索引服务：文档和记忆新增/更新时只把条目入队，本服务在后台生成
/// 分块 embedding 并写入向量索引——写路径不再被网络调用阻塞。
/// 队列按条目去重（处理中的条目可再次入队以反映新内容），容量 1000，
/// 满载时非阻塞跳过并写入 <see cref="LastError"/>；带 3 次退避重试。
/// 未配置 embedding 模型时队列被消费但直接跳过。
/// </summary>
public sealed class DocumentIndexingService : BackgroundService
{
    private const int QueueCapacity = 1000;
    private readonly Channel<(string Id, bool IsMemory)> _queue = Channel.CreateBounded<(string, bool)>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
    private readonly object _queueLock = new();
    private readonly HashSet<(string Id, bool IsMemory)> _queued = new();

    private readonly KnowledgeBaseService _service;
    private readonly VectorIndexService? _vectors;
    private readonly EmbeddingClient? _embedder;
    private readonly TimeSpan _retryDelay;

    private int _pending;

    /// <summary>等待索引的条目数（队列按条目去重，原子计数器维护）。</summary>
    public int PendingCount => Volatile.Read(ref _pending);

    /// <summary>最近一次索引失败的原因（成功后清空）。</summary>
    public string? LastError { get; private set; }

    public DocumentIndexingService(KnowledgeBaseService service,
        VectorIndexService? vectors = null, EmbeddingClient? embedder = null, TimeSpan? retryDelay = null)
    {
        _service = service;
        _vectors = vectors;
        _embedder = embedder;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>把文档加入后台索引队列（非阻塞，立即返回）。</summary>
    public void Enqueue(string documentId) => Enqueue(documentId, false);

    public void EnqueueMemory(string memoryId) => Enqueue(memoryId, true);

    public void RemoveMemory(string memoryId) =>
        _vectors?.Remove(VectorIndexService.MemoryIndexKey(memoryId));

    private void Enqueue(string id, bool isMemory)
    {
        lock (_queueLock)
        {
            var item = (Id: id, IsMemory: isMemory);
            if (!_queued.Add(item))
                return;
            if (_queue.Writer.TryWrite(item))
            {
                Interlocked.Increment(ref _pending);
                return;
            }
            _queued.Remove(item);
            LastError = $"索引队列已满（{QueueCapacity}），暂时跳过{(isMemory ? "记忆" : "文档")} {id}。" +
                        "可稍后调用 POST /api/ai/index/rebuild 重建。";
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (id, isMemory) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            lock (_queueLock)
            {
                _queued.Remove((id, isMemory));
                Interlocked.Decrement(ref _pending);
            }
            if (_embedder is null || _vectors is null)
                continue;

            var indexId = isMemory ? VectorIndexService.MemoryIndexKey(id) : id;
            var title = isMemory ? _service.GetMemoryEntry(id)?.Title : _service.GetDocument(id)?.Title;
            var content = isMemory ? _service.GetMemoryEntry(id)?.Content : _service.GetDocument(id)?.Content;
            if (title is null || content is null)
                continue;
            if (string.IsNullOrWhiteSpace(content))
            {
                _vectors.Remove(indexId);
                continue;
            }

            var indexedSuccessfully = false;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var chunks = KnowledgeBaseService.ChunkTextOnBoundaries(title + "\n" + content);
                    var vectors = await _embedder.EmbedBatchedAsync(chunks, cancellationToken: stoppingToken);
                    if (vectors is { Count: > 0 } && vectors.Count == chunks.Count && vectors.All(vector => vector.Length > 0))
                    {
                        var currentTitle = isMemory ? _service.GetMemoryEntry(id)?.Title : _service.GetDocument(id)?.Title;
                        var currentContent = isMemory ? _service.GetMemoryEntry(id)?.Content : _service.GetDocument(id)?.Content;
                        if (currentTitle != title || currentContent != content)
                            break;
                        _vectors.SetChunks(indexId, _embedder.Model, chunks.Select((text, index) =>
                            new VectorChunk { Index = index, Text = text, Vector = vectors[index] }).ToList());
                        LastError = null;
                        indexedSuccessfully = true;
                        Console.WriteLine($"[Indexing] 已写入 {vectors.Count} 个文本块向量");
                        break;
                    }

                    LastError = $"第 {attempt} 次索引失败：{_embedder.LastError ?? "embedding 返回空或数量不匹配"}";
                    if (attempt < 3)
                        await Task.Delay(_retryDelay * attempt, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LastError = $"第 {attempt} 次索引失败：{ex.Message}";
                    if (attempt < 3)
                        await Task.Delay(_retryDelay * attempt, stoppingToken);
                }
            }

            if (!indexedSuccessfully)
                _vectors.Remove(indexId);
        }
    }
}
