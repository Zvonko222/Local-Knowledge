using Microsoft.AspNetCore.Mvc;
using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;

namespace LocalKnowledgeBase.Api.Controllers;

[ApiController]
[Route("api/knowledge-base")]
public sealed class KnowledgeBaseController : ControllerBase
{
    private readonly KnowledgeBaseService _service;
    private readonly VectorIndexService? _vectors;
    private readonly EmbeddingClient? _embedder;
    private readonly RetrievalProfileService? _profiles;

    public KnowledgeBaseController(KnowledgeBaseService service,
        VectorIndexService? vectors = null, EmbeddingClient? embedder = null,
        RetrievalProfileService? profiles = null)
    {
        _service = service;
        _vectors = vectors;
        _embedder = embedder;
        _profiles = profiles;
    }

    /// <summary>为文档生成向量并写入索引（未配置 embedding 时跳过，失败不影响主流程）。</summary>
    private async Task TryIndexDocumentAsync(Document document)
    {
        if (_embedder is null || _vectors is null || string.IsNullOrEmpty(document.Content))
            return;
        try
        {
            var vector = await _embedder.EmbedAsync(document.Title + "\n" + document.Content);
            if (vector is { Length: > 0 })
                _vectors.Set(document.Id, _embedder.Model, vector);
        }
        catch
        {
            // 向量索引失败不影响文档主流程；可用 POST /api/ai/index/rebuild 重建
        }
    }

    /// <summary>混合检索的查询向量：embedding 未配置、索引为空或模型不一致时返回 null（退化纯关键词）。</summary>
    private async Task<float[]?> TryEmbedQueryAsync(string query)
    {
        if (_embedder is null || _vectors is null || _vectors.Count == 0)
            return null;
        if (_vectors.StoredModel != _embedder.Model)
            return null; // 索引由其他模型生成，需 POST /api/ai/index/rebuild 重建
        try
        {
            return await _embedder.EmbedAsync(query);
        }
        catch
        {
            return null;
        }
    }

    #region Status

    [HttpGet("status")]
    public ActionResult<KnowledgeBaseStatus> GetStatus()
    {
        return Ok(new KnowledgeBaseStatus(
            "Local Knowledge Base",
            "Phase 3: Knowledge Base Features",
            _service.DataDirectory));
    }

    #endregion

    #region Data Directory

    [HttpPost("data-directory")]
    public ActionResult SetDataDirectory([FromBody] SetDataDirectoryRequest request)
    {
        try
        {
            _service.Reconfigure(request.Path);
            KnowledgeBaseService.SetUserDataDirectory(request.Path);
            _vectors?.Reconfigure(request.Path); // 向量索引跟随数据目录切换
            return Ok(new { message = "Data directory updated." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("data-directory")]
    public ActionResult<GetDataDirectoryResponse> GetDataDirectory()
    {
        var path = _service.DataDirectory;
        return Ok(new GetDataDirectoryResponse(path, !string.IsNullOrEmpty(path)));
    }

    #endregion

    #region Documents

    [HttpGet("documents")]
    public ActionResult<List<Document>> GetDocuments()
    {
        try
        {
            var documents = _service.ListDocuments();
            return Ok(documents);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("documents/{id}")]
    public ActionResult<Document> GetDocument(string id)
    {
        var document = _service.GetDocument(id);
        if (document == null)
            return NotFound(new { error = "Document not found" });

        return Ok(document);
    }

    [HttpPost("documents")]
    public async Task<ActionResult<Document>> AddDocument([FromBody] AddDocumentRequest request)
    {
        try
        {
            var dataDir = _service.DataDirectory;
            if (string.IsNullOrEmpty(dataDir))
                return BadRequest(new { error = "Data directory not configured" });

            var originalFileName = Path.GetFileName(request.FileName);
            if (string.IsNullOrWhiteSpace(originalFileName))
                return BadRequest(new { error = "File name is required" });

            var document = new Document
            {
                Title = string.IsNullOrWhiteSpace(request.Title)
                    ? Path.GetFileNameWithoutExtension(originalFileName)
                    : request.Title.Trim(),
                FileType = Path.GetExtension(originalFileName),
                OriginalFileName = originalFileName,
                StoredFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(originalFileName),
                Tags = KnowledgeBaseService.NormalizeTagsPublic(request.Tags)
            };

            // Write raw file
            var rawDir = Path.Combine(dataDir, "raw_files");
            Directory.CreateDirectory(rawDir);
            var filePath = Path.Combine(rawDir, document.StoredFileName);
            await System.IO.File.WriteAllBytesAsync(filePath, request.FileContent);

            // Extract text
            var content = KnowledgeBaseService.ExtractTextFromFile(filePath, request.FileContent);

            document.FileSize = request.FileContent.Length;
            document.Content = content;

            _service.AddDocument(document);
            await TryIndexDocumentAsync(document); // 配置了 embedding 时同步写入向量索引
            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPut("documents/{id}")]
    public async Task<ActionResult<Document>> UpdateDocument(string id, [FromBody] UpdateDocumentRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { error = "Title is required" });

            var document = _service.UpdateDocument(id, request.Title, request.Content ?? string.Empty, request.Tags);
            if (document == null)
                return NotFound(new { error = "Document not found" });

            await TryIndexDocumentAsync(document); // 正文变化后刷新向量
            return Ok(document);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("documents/{id}")]
    public ActionResult DeleteDocument(string id)
    {
        try
        {
            var document = _service.GetDocument(id);
            if (document == null)
                return NotFound(new { error = "Document not found" });

            if (_service.DeleteDocument(id))
            {
                _vectors?.Remove(id); // 同步移除向量
                return Ok(new { message = "Document deleted" });
            }

            return StatusCode(500, new { error = "Failed to delete" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Search

    [HttpGet("search")]
    public async Task<ActionResult<SearchResults>> Search([FromQuery] string q, [FromQuery] int? max = null)
    {
        try
        {
            // 混合检索：配置了 embedding 时自动叠加语义相似度；未配置时为纯关键词检索
            // max 未显式指定时使用当前检索预算档位
            var maxResults = Math.Clamp(max ?? (_profiles?.Get().MaxResults ?? 20), 1, 100);
            var queryVector = await TryEmbedQueryAsync(q);
            var results = _service.SearchDocuments(q, maxResults, _vectors, queryVector);
            return Ok(new SearchResults(q, results));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Memory

    [HttpGet("memory")]
    public ActionResult<List<MemoryEntry>> GetMemoryEntries()
    {
        try
        {
            var entries = _service.ListMemoryEntries();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("memory/{id}")]
    public ActionResult<MemoryEntry> GetMemoryEntry(string id)
    {
        var entry = _service.GetMemoryEntry(id);
        if (entry == null)
            return NotFound(new { error = "Memory entry not found" });

        return Ok(entry);
    }

    [HttpPost("memory")]
    public ActionResult<MemoryEntry> AddMemoryEntry([FromBody] AddMemoryEntryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { error = "Title and content are required" });

            var entry = new MemoryEntry
            {
                Title = request.Title,
                Content = request.Content,
                Tags = request.Tags
            };

            _service.AddMemoryEntry(entry);
            return CreatedAtAction(nameof(GetMemoryEntry), new { id = entry.Id }, entry);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPut("memory/{id}")]
    public ActionResult<MemoryEntry> UpdateMemoryEntry(string id, [FromBody] UpdateMemoryEntryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { error = "Title and content are required" });

            var entry = _service.UpdateMemoryEntry(id, request.Title, request.Content, request.Tags);
            if (entry == null)
                return NotFound(new { error = "Memory entry not found" });

            return Ok(entry);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("memory/{id}")]
    public ActionResult DeleteMemoryEntry(string id)
    {
        try
        {
            var entry = _service.GetMemoryEntry(id);
            if (entry == null)
                return NotFound(new { error = "Memory entry not found" });

            if (_service.DeleteMemoryEntry(id))
                return Ok(new { message = "Memory entry deleted" });

            return StatusCode(500, new { error = "Failed to delete" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("memory/search")]
    public ActionResult<List<MemoryEntry>> SearchMemory([FromQuery] string q)
    {
        try
        {
            var results = _service.SearchMemoryEntries(q);
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion
}

// DTOs
public sealed record KnowledgeBaseStatus(string Name, string Stage, string Mode);
public sealed record SetDataDirectoryRequest(string Path);
public sealed record GetDataDirectoryResponse(string? Path, bool Configured);
public sealed record AddDocumentRequest(string? Title, string? FileName, byte[] FileContent, string? Tags = null);
public sealed record UpdateDocumentRequest(string Title, string? Content, string? Tags = null);
public sealed record AddMemoryEntryRequest(string Title, string Content, string? Tags = null);
public sealed record UpdateMemoryEntryRequest(string Title, string Content, string? Tags = null);
public sealed record SearchResults(string Query, List<Document> Results);
