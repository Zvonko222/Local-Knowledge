using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;
using LocalKnowledgeBase.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalKnowledgeBase.Api.Controllers;

/// <summary>
/// AI 专用接口：让任何 AI 工具/工作流（HTTP 工具调用、Dify、n8n、OpenWebUI 等）
/// 都能把本地知识库当作检索与问答服务，而不必支持 MCP。
/// 与 MCP 服务共用同一个 API 与数据（API 是唯一写入方）。
/// </summary>
[ApiController]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private readonly KnowledgeBaseService _service;
    private readonly LocalLlmClient? _llm;
    private readonly VectorIndexService? _vectors;
    private readonly EmbeddingClient? _embedder;
    private readonly RetrievalProfileService? _profiles;
    private readonly Services.DocumentIndexingService? _indexing;

    public AiController(KnowledgeBaseService service, LocalLlmClient? llm = null,
        VectorIndexService? vectors = null, EmbeddingClient? embedder = null,
        RetrievalProfileService? profiles = null, Services.DocumentIndexingService? indexing = null)
    {
        _service = service;
        _llm = llm;
        _vectors = vectors;
        _embedder = embedder;
        _profiles = profiles;
        _indexing = indexing;
    }

    /// <summary>当前检索预算档位与可选预设（含各档 token 比重）。</summary>
    [HttpGet("profile")]
    public ActionResult<object> GetProfile()
    {
        var profile = _profiles?.Get() ?? RetrievalProfile.Standard;
        return Ok(new
        {
            preset = profile.Preset,
            maxResults = profile.MaxResults,
            snippetLength = profile.SnippetLength,
            maxTotalChars = profile.MaxTotalChars,
            tokenWeight = profile.TokenWeight,
            presets = new[]
            {
                new { preset = RetrievalProfile.Light.Preset, maxResults = RetrievalProfile.Light.MaxResults,
                      maxTotalChars = RetrievalProfile.Light.MaxTotalChars, tokenWeight = RetrievalProfile.Light.TokenWeight },
                new { preset = RetrievalProfile.Standard.Preset, maxResults = RetrievalProfile.Standard.MaxResults,
                      maxTotalChars = RetrievalProfile.Standard.MaxTotalChars, tokenWeight = RetrievalProfile.Standard.TokenWeight },
                new { preset = RetrievalProfile.Deep.Preset, maxResults = RetrievalProfile.Deep.MaxResults,
                      maxTotalChars = RetrievalProfile.Deep.MaxTotalChars, tokenWeight = RetrievalProfile.Deep.TokenWeight }
            }
        });
    }

    /// <summary>向量索引状态：模型、待处理队列、已索引数量、块数与最近错误。</summary>
    [HttpGet("index/status")]
    public ActionResult<object> GetIndexStatus()
    {
        var documents = _service.ListDocuments();
        var memories = _service.ListMemoryEntries();
        return Ok(new
        {
            embeddingConfigured = _embedder is not null,
            model = _embedder?.Model,
            pending = _indexing?.PendingCount ?? 0,
            indexed = _vectors?.Count ?? 0,
            indexedDocuments = _vectors is null ? 0 : documents.Count(document => _vectors.Get(document.Id) is not null),
            indexedMemories = _vectors is null ? 0 : memories.Count(memory => _vectors.Get(VectorIndexService.MemoryIndexKey(memory.Id)) is not null),
            chunks = _vectors is null ? 0
                : documents.Sum(document => _vectors.GetChunks(document.Id).Count)
                  + memories.Sum(memory => _vectors.GetChunks(VectorIndexService.MemoryIndexKey(memory.Id)).Count),
            lastError = _indexing?.LastError
        });
    }

    /// <summary>切换检索预算档位（light / standard / deep）。</summary>
    [HttpPut("profile")]
    public ActionResult<object> SetProfile([FromBody] ProfileRequest request)
    {
        if (_profiles is null)
            return StatusCode(503, new { error = "档位服务不可用。" });

        var profile = _profiles.Set(request.Preset);
        return Ok(new
        {
            preset = profile.Preset,
            maxResults = profile.MaxResults,
            snippetLength = profile.SnippetLength,
            maxTotalChars = profile.MaxTotalChars,
            tokenWeight = profile.TokenWeight
        });
    }

    /// <summary>混合检索的查询向量（embedding 未配置/索引为空/模型不一致时返回 null）。</summary>
    private async Task<float[]?> TryEmbedQueryAsync(string query, CancellationToken cancellationToken)
    {
        if (_embedder is null || _vectors is null || _vectors.Count == 0)
            return null;
        if (_vectors.StoredModel != _embedder.Model)
            return null;
        try
        {
            return await _embedder.EmbedAsync(query, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static List<AiContextItem> MergeContextItems(IReadOnlyList<Document> documents,
        IReadOnlyList<MemoryEntry> memories, int max)
    {
        var items = new List<AiContextItem>(max);
        for (var index = 0; items.Count < max && (index < documents.Count || index < memories.Count); index++)
        {
            if (index < documents.Count)
            {
                var document = documents[index];
                items.Add(new AiContextItem("document", document.Id, document.Title, document.Content));
            }
            if (items.Count < max && index < memories.Count)
            {
                var memory = memories[index];
                items.Add(new AiContextItem("memory", memory.Id, memory.Title, memory.Content));
            }
        }
        return items;
    }

    private IReadOnlyDictionary<string, string>? BuildSemanticSnippets(IReadOnlyList<Document> documents,
        IReadOnlyList<MemoryEntry> memories, float[]? queryVector)
    {
        if (queryVector is not { Length: > 0 } || _vectors is null)
            return null;

        var snippets = new Dictionary<string, string>();
        foreach (var (id, key) in documents.Select(document => (document.Id, document.Id))
                     .Concat(memories.Select(memory => (memory.Id, VectorIndexService.MemoryIndexKey(memory.Id)))))
        {
            var match = _vectors.GetMostSimilarChunk(key, queryVector);
            if (match is { Similarity: > 0 } && !string.IsNullOrWhiteSpace(match.Value.Chunk.Text))
                snippets[key] = match.Value.Chunk.Text;
        }
        return snippets;
    }

    /// <summary>服务状态：AI 接入前可先探测（模型是否已配置、数据目录位置）。</summary>
    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        return Ok(new
        {
            llmConfigured = _llm is not null,
            llmModel = _llm?.Model,
            llmBaseUrl = _llm?.BaseUrl,
            dataDirectory = _service.DataDirectory
        });
    }

    /// <summary>
    /// 检索知识库并打包成可直接注入大模型提示词的上下文（带 [n] 编号引用）。
    /// 适合“自己拿着上下文去调任意模型”的工具。
    /// </summary>
    [HttpPost("context")]
    public async Task<ActionResult<AiContextResponse>> GetContext([FromBody] AiContextRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query is required" });

        var profile = _profiles?.Get() ?? RetrievalProfile.Standard;
        var max = Math.Clamp(request.Max ?? profile.MaxResults, 1, 50);
        var queryVector = await TryEmbedQueryAsync(request.Query, cancellationToken);
        var documents = _service.SearchDocuments(request.Query, max, _vectors, queryVector);
        var memories = _service.SearchMemoryEntries(request.Query, max, _vectors, queryVector);
        var items = MergeContextItems(documents, memories, max);
        var context = AiContextBuilder.Build(request.Query, items,
            Math.Clamp(request.SnippetLength ?? profile.SnippetLength, 50, 5000),
            Math.Clamp(request.MaxTotalChars ?? profile.MaxTotalChars, 200, 50000),
            BuildSemanticSnippets(documents, memories, queryVector));

        return Ok(new AiContextResponse(
            context.Query,
            context.TotalHits,
            context.IncludedCount,
            context.Truncated,
            context.ContextText,
            context.Sources
                .Select(s => new AiSourceDto(s.Index, s.Title, s.DocumentId, s.SourceType, s.MemoryEntryId))
                .ToList(),
            queryVector is { Length: > 0 }));
    }

    /// <summary>
    /// 完整问答：检索知识库 + 调用本地大模型生成带引用的回答。
    /// 需要先通过环境变量配置本地模型；未配置返回 503。
    /// </summary>
    [HttpPost("answer")]
    public async Task<ActionResult<AiAnswerResponse>> GetAnswer([FromBody] AiAnswerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query is required" });

        if (_llm is null)
            return StatusCode(503, new
            {
                error = "本地模型未配置。请设置环境变量 " + LocalLlmClient.UrlEnvVar +
                        "（如 http://localhost:11434/v1）与 " + LocalLlmClient.ModelEnvVar +
                        "（如 qwen2.5:7b）后重启 API。"
            });

        var profile = _profiles?.Get() ?? RetrievalProfile.Standard;
        var max = Math.Clamp(request.Max ?? profile.MaxResults, 1, 50);
        var queryVector = await TryEmbedQueryAsync(request.Query, cancellationToken);
        var documents = _service.SearchDocuments(request.Query, max, _vectors, queryVector);
        var memories = _service.SearchMemoryEntries(request.Query, max, _vectors, queryVector);
        var items = MergeContextItems(documents, memories, max);
        if (items.Count == 0)
            return Ok(new AiAnswerResponse(
                $"知识库中没有找到与「{request.Query}」相关的资料。",
                new List<AiSourceDto>(), _llm.Model, queryVector is { Length: > 0 }));

        var context = AiContextBuilder.Build(request.Query, items,
            Math.Clamp(request.SnippetLength ?? profile.SnippetLength, 50, 5000),
            Math.Clamp(request.MaxTotalChars ?? profile.MaxTotalChars, 200, 50000),
            BuildSemanticSnippets(documents, memories, queryVector));
        var userPrompt = context.ContextText + "\n\n问题：" + request.Query;

        string answer;
        try
        {
            answer = await _llm.CompleteAsync(AiContextBuilder.SystemPrompt, userPrompt, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "调用本地模型失败：" + ex.Message });
        }

        return Ok(new AiAnswerResponse(
            answer.Trim(),
            context.Sources
                .Select(s => new AiSourceDto(s.Index, s.Title, s.DocumentId, s.SourceType, s.MemoryEntryId))
                .ToList(),
            _llm.Model,
            queryVector is { Length: > 0 }));
    }
    /// <summary>
    /// 与指定文档语义最相关的其他文档（基于向量索引，按相似度降序）。
    /// embedding 未配置返回 503；文档未建向量时返回空列表。
    /// </summary>
    [HttpGet("related/{id}")]
    public ActionResult<object> GetRelated(string id, [FromQuery] int max = 5)
    {
        if (_embedder is null || _vectors is null)
            return StatusCode(503, new
            {
                error = "未配置 embedding 模型。请设置环境变量 LOCAL_KNOWLEDGE_BASE_EMBED_MODEL 后重启 API。"
            });
        if (_service.GetDocument(id) is null)
            return NotFound(new { error = "Document not found" });

        var related = _vectors.FindMostSimilar(id, Math.Clamp(max, 1, 20))
            .Select(x => new
            {
                title = _service.GetDocument(x.DocumentId)?.Title ?? "(已删除)",
                documentId = x.DocumentId,
                similarity = Math.Round(x.Similarity, 4)
            })
            .ToList();

        return Ok(new { documentId = id, related });
    }

    /// <summary>
    /// 重建向量索引：对全部文档重新生成 embedding（换 embedding 模型后必须调用）。
    /// 未配置 embedding 模型返回 503。
    /// </summary>
    [HttpPost("index/rebuild")]
    public async Task<ActionResult<object>> RebuildIndex(CancellationToken cancellationToken)
    {
        if (_embedder is null || _vectors is null)
            return StatusCode(503, new
            {
                error = "未配置 embedding 模型。请设置环境变量 LOCAL_KNOWLEDGE_BASE_EMBED_MODEL" +
                        "（如 embedding-3、nomic-embed-text）后重启 API。"
            });

        var documents = _service.ListDocuments();
        var memories = _service.ListMemoryEntries();
        var indexedDocuments = 0;
        var indexedMemories = 0;
        var failed = 0;
        foreach (var (id, title, content, isMemory) in documents
                     .Select(document => (document.Id, document.Title, document.Content, false))
                     .Concat(memories.Select(memory => (memory.Id, memory.Title, memory.Content, true))))
        {
            var indexId = isMemory ? VectorIndexService.MemoryIndexKey(id) : id;
            if (string.IsNullOrWhiteSpace(content))
            {
                _vectors.Remove(indexId);
                failed++;
                continue;
            }
            try
            {
                var chunks = KnowledgeBaseService.ChunkTextOnBoundaries(title + "\n" + content);
                var vectors = await _embedder.EmbedBatchedAsync(chunks, cancellationToken: cancellationToken);
                if (vectors is { Count: > 0 } && vectors.Count == chunks.Count)
                {
                    _vectors.SetChunks(indexId, _embedder.Model, chunks.Select((text, index) =>
                        new VectorChunk { Index = index, Text = text, Vector = vectors[index] }).ToList());
                    if (isMemory)
                        indexedMemories++;
                    else
                        indexedDocuments++;
                }
                else
                {
                    _vectors.Remove(indexId);
                    failed++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                _vectors.Remove(indexId);
                failed++;
            }
        }

        var validIds = documents.Select(document => document.Id)
            .Concat(memories.Select(memory => VectorIndexService.MemoryIndexKey(memory.Id)));
        _vectors.RemoveExcept(validIds);

        return Ok(new
        {
            indexed = indexedDocuments + indexedMemories,
            indexedDocuments,
            indexedMemories,
            failed,
            total = documents.Count + memories.Count,
            chunks = validIds.Sum(id => _vectors.GetChunks(id).Count),
            model = _embedder.Model
        });
    }
}

// DTOs
public sealed record AiContextRequest(string Query, int? Max = null, int? SnippetLength = null, int? MaxTotalChars = null);
public sealed record ProfileRequest(string Preset);
public sealed record AiAnswerRequest(string Query, int? Max = null, int? SnippetLength = null, int? MaxTotalChars = null);
public sealed record AiSourceDto(int Index, string Title, string DocumentId, string SourceType = "document", string? MemoryEntryId = null);
public sealed record AiContextResponse(string Query, int TotalHits, int IncludedCount, bool Truncated, string Context, List<AiSourceDto> Sources, bool SemanticUsed = false);
public sealed record AiAnswerResponse(string Answer, List<AiSourceDto> Sources, string Model, bool SemanticUsed = false);
