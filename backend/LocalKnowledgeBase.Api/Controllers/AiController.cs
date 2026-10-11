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

    public AiController(KnowledgeBaseService service, LocalLlmClient? llm = null,
        VectorIndexService? vectors = null, EmbeddingClient? embedder = null,
        RetrievalProfileService? profiles = null)
    {
        _service = service;
        _llm = llm;
        _vectors = vectors;
        _embedder = embedder;
        _profiles = profiles;
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
    private async Task<float[]?> TryEmbedQueryAsync(string query)
    {
        if (_embedder is null || _vectors is null || _vectors.Count == 0)
            return null;
        if (_vectors.StoredModel != _embedder.Model)
            return null;
        try
        {
            return await _embedder.EmbedAsync(query);
        }
        catch
        {
            return null;
        }
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
    public async Task<ActionResult<AiContextResponse>> GetContext([FromBody] AiContextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query is required" });

        var profile = _profiles?.Get() ?? RetrievalProfile.Standard;
        var queryVector = await TryEmbedQueryAsync(request.Query);
        var documents = _service.SearchDocuments(request.Query,
            Math.Clamp(request.Max ?? profile.MaxResults, 1, 50), _vectors, queryVector);
        var context = AiContextBuilder.Build(request.Query, documents,
            Math.Clamp(request.SnippetLength ?? profile.SnippetLength, 50, 5000),
            Math.Clamp(request.MaxTotalChars ?? profile.MaxTotalChars, 200, 50000));

        return Ok(new AiContextResponse(
            context.Query,
            context.TotalHits,
            context.IncludedCount,
            context.Truncated,
            context.ContextText,
            context.Sources
                .Select(s => new AiSourceDto(s.Index, s.Title, s.DocumentId))
                .ToList()));
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
        var queryVector = await TryEmbedQueryAsync(request.Query);
        var documents = _service.SearchDocuments(request.Query,
            Math.Clamp(request.Max, 1, 50), _vectors, queryVector);
        if (documents.Count == 0)
            return Ok(new AiAnswerResponse(
                $"知识库中没有找到与「{request.Query}」相关的资料。",
                new List<AiSourceDto>(), _llm.Model));

        var context = AiContextBuilder.Build(request.Query, documents, AiContextBuilder.DefaultSnippetLength, 4000);
        var userPrompt = context.ContextText + "\n\n问题：" + request.Query;

        string answer;
        try
        {
            answer = await _llm.CompleteAsync(AiContextBuilder.SystemPrompt, userPrompt, cancellationToken);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "调用本地模型失败：" + ex.Message });
        }

        return Ok(new AiAnswerResponse(
            answer.Trim(),
            context.Sources
                .Select(s => new AiSourceDto(s.Index, s.Title, s.DocumentId))
                .ToList(),
            _llm.Model));
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
        var indexed = 0;
        foreach (var document in documents)
        {
            if (string.IsNullOrEmpty(document.Content))
                continue;
            try
            {
                var vector = await _embedder.EmbedAsync(document.Title + "\n" + document.Content, cancellationToken);
                if (vector is { Length: > 0 })
                {
                    _vectors.Set(document.Id, _embedder.Model, vector);
                    indexed++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // 单篇失败跳过，继续其余文档
            }
        }
        _vectors.RemoveExcept(documents.Select(d => d.Id));

        return Ok(new { indexed, total = documents.Count, model = _embedder.Model });
    }
}

// DTOs
public sealed record AiContextRequest(string Query, int? Max = null, int? SnippetLength = null, int? MaxTotalChars = null);
public sealed record ProfileRequest(string Preset);
public sealed record AiAnswerRequest(string Query, int Max = 6);
public sealed record AiSourceDto(int Index, string Title, string DocumentId);
public sealed record AiContextResponse(string Query, int TotalHits, int IncludedCount, bool Truncated, string Context, List<AiSourceDto> Sources);
public sealed record AiAnswerResponse(string Answer, List<AiSourceDto> Sources, string Model);
