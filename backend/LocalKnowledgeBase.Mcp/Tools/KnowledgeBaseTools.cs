using System.ComponentModel;
using System.Text;
using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;
using LocalKnowledgeBase.Mcp.Api;
using ModelContextProtocol.Server;

namespace LocalKnowledgeBase.Mcp.Tools;

/// <summary>
/// 本地知识库暴露给 AI 客户端的 MCP 工具。
/// 命名统一使用 kb_ 前缀，避免与其他 MCP 服务器的工具重名。
/// </summary>
[McpServerToolType]
public sealed class KnowledgeBaseTools(KnowledgeBaseApiClient api)
{
    [McpServerTool(Name = "kb_set_profile")]
    [Description("切换检索预算档位，控制每次检索的 token 消耗，对所有检索入口生效：light=轻量（约 0.4×，省 token）、standard=标准（1×）、deep=深度（约 2×，更详尽）。")]
    public string SetProfile(
        [Description("档位名：light / standard / deep")] string preset)
    {
        var profile = api.SetProfileAsync(preset.Trim()).GetAwaiter().GetResult();
        if (profile is null)
            return Error(api.LastError ?? "本地知识库 API 不可达。");

        return Json.Serialize(new
        {
            已切换 = true,
            档位 = profile.Preset,
            最大条数 = profile.MaxResults,
            单条摘要字符 = profile.SnippetLength,
            总字符预算 = profile.MaxTotalChars,
            token比重 = $"约 {profile.TokenWeight}×"
        });
    }

    [McpServerTool(Name = "kb_status")]
    [Description("查看本地知识库服务状态：是否在线、数据目录位置。排查连接问题时先调用它。")]
    public string GetStatus()
    {
        var status = api.GetStatusAsync().GetAwaiter().GetResult();
        if (status is null)
            return Error(api.LastError);

        return Json.Serialize(new
        {
            在线 = true,
            status.Name,
            数据目录 = status.Mode,
            api.BaseUrl
        });
    }

    [McpServerTool(Name = "kb_search_documents")]
    [Description("在本地知识库中全文检索文档（匹配标题、正文、标签）。返回命中文档的编号、标题、标签与内容摘要。")]
    public string SearchDocuments(
        [Description("搜索关键词，支持中英文")] string query,
        [Description("最多返回条数，1-50；留空则使用当前预算档位")] int? max = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Error("搜索关键词不能为空。");

        var clamped = max is { } m ? Math.Clamp(m, 1, 50) : (int?)null;
        var response = api.SearchDocumentsAsync(query.Trim(), clamped).GetAwaiter().GetResult();
        if (response is null)
            return Error(api.LastError);

        // 未显式指定条数时限制最多 20 条，避免 deep 档位意外放大返回量
        var documents = response.Results;
        if (clamped is null && documents.Count > 20)
            documents = documents.Take(20).ToList();

        var terms = KnowledgeBaseService.TokenizeQuery(response.Query);
        var items = documents.Select(document => new
        {
            编号 = document.Id,
            标题 = document.Title,
            标签 = document.Tags,
            类型 = document.FileType,
            更新时间 = document.UpdatedAt.LocalDateTime,
            内容摘要 = KnowledgeBaseService.BestSnippet(document.Content, terms, 400)
        });

        return Json.Serialize(new
        {
            关键词 = response.Query,
            命中数 = documents.Count,
            结果 = items
        });
    }

    [McpServerTool(Name = "kb_list_documents")]
    [Description("列出本地知识库中的全部文档（编号、标题、标签、大小、更新时间），不含正文。")]
    public string ListDocuments()
    {
        var documents = api.GetDocumentsAsync().GetAwaiter().GetResult();
        if (documents is null)
            return Error(api.LastError);

        var items = documents.Select(document => new
        {
            编号 = document.Id,
            标题 = document.Title,
            标签 = document.Tags,
            类型 = document.FileType,
            大小 = document.FileSize,
            更新时间 = document.UpdatedAt.LocalDateTime
        });

        return Json.Serialize(new { 文档总数 = documents.Count, 文档 = items });
    }

    [McpServerTool(Name = "kb_get_document")]
    [Description("读取本地知识库中一篇文档的完整内容。编号来自 kb_search_documents 或 kb_list_documents。")]
    public string GetDocument([Description("文档编号")] string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Error("文档编号不能为空。");

        var document = api.GetDocumentAsync(id.Trim()).GetAwaiter().GetResult();
        if (document is null)
            return Error(api.LastError ?? $"未找到编号为 {id} 的文档。");

        return Json.Serialize(new
        {
            编号 = document.Id,
            标题 = document.Title,
            标签 = document.Tags,
            类型 = document.FileType,
            原始文件名 = document.OriginalFileName,
            更新时间 = document.UpdatedAt.LocalDateTime,
            正文 = document.Content
        });
    }

    [McpServerTool(Name = "kb_get_context")]
    [Description("检索本地知识库并把结果打包成可直接注入大模型提示词的上下文文本（带 [n] 编号引用、按相关度排序）。适合在生成回答前快速取上下文。")]
    public string GetContext(
        [Description("搜索关键词或整句提问")] string query,
        [Description("最多返回条数，1-20；留空则使用当前预算档位")] int? max = null,
        [Description("上下文总字符预算；留空则使用当前预算档位。超出预算的低相关条目会被舍弃，用于控制 token 消耗")] int? maxChars = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Error("搜索关键词不能为空。");

        var context = api.GetAiContextAsync(query.Trim(), max, maxChars).GetAwaiter().GetResult();
        if (context is null)
            return Error(api.LastError ?? "本地知识库 API 不可达。");

        if (context.TotalHits == 0)
            return Json.Serialize(new { 提示 = $"知识库中没有找到与「{query}」相关的资料。", 命中数 = 0 });

        var sources = context.Sources.Select(s => new { 序号 = s.Index, 标题 = s.Title, 文档编号 = s.DocumentId });
        return Json.Serialize(new
        {
            提示 = "以下上下文可直接拼进大模型提示词使用。",
            命中数 = context.TotalHits,
            已装入条数 = context.IncludedCount,
            因预算舍弃 = context.Truncated,
            上下文 = context.Context,
            来源 = sources
        });
    }

    [McpServerTool(Name = "kb_related_documents")]
    [Description("找出知识库中与指定文档语义最相关的其他文档（基于向量相似度）。适合在阅读/总结一篇文档时拉取相关资料。")]
    public string GetRelatedDocuments(
        [Description("文档编号")] string id,
        [Description("最多返回条数，1-20，默认 5")] int max = 5)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Error("文档编号不能为空。");

        var response = api.GetRelatedAsync(id.Trim(), Math.Clamp(max, 1, 20)).GetAwaiter().GetResult();
        if (response is null)
            return Error(api.LastError ?? "本地知识库 API 不可达。");

        var related = response.Related.Select(r => new
        {
            编号 = r.DocumentId,
            标题 = r.Title,
            相似度 = $"{r.Similarity:P0}"
        });

        return Json.Serialize(new
        {
            文档编号 = response.DocumentId,
            相关文档数 = response.Related.Count,
            相关文档 = related
        });
    }

    [McpServerTool(Name = "kb_add_document")]
    [Description("把一段文本内容保存为新文档加入本地知识库（适合沉淀结论、整理的笔记、收集的资料）。保存后即可被 kb_search_documents 检索。")]
    public string AddDocument(
        [Description("文档标题（必填）")] string title,
        [Description("文件名（必填），建议以 .md 或 .txt 结尾；不要用 .pdf/.docx，那两种只用于导入真实文件")] string fileName,
        [Description("文档正文（纯文本或 Markdown，必填）")] string content,
        [Description("标签，逗号分隔，可留空，例如“架构,部署”")] string? tags = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error("文档标题不能为空。");
        if (string.IsNullOrWhiteSpace(fileName))
            return Error("文件名不能为空。");
        if (string.IsNullOrWhiteSpace(content))
            return Error("文档内容不能为空。");

        // 确保扩展名安全（API 端也会处理），默认当作 Markdown
        var safeFileName = Path.GetFileName(fileName.Trim());
        if (Path.GetExtension(safeFileName) is "" or null)
            safeFileName += ".md";

        var created = api.AddDocumentAsync(
            title.Trim(), safeFileName, Encoding.UTF8.GetBytes(content), tags).GetAwaiter().GetResult();
        if (created is null)
            return Error(api.LastError ?? "保存失败。");

        return Json.Serialize(new
        {
            已保存 = true,
            编号 = created.Id,
            标题 = created.Title,
            文件名 = safeFileName
        });
    }

    [McpServerTool(Name = "kb_search_memory")]
    [Description("在项目记忆（用户的长期上下文：决策、偏好、约定等）中检索条目。")]
    public string SearchMemory([Description("搜索关键词")] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Error("搜索关键词不能为空。");

        var entries = api.SearchMemoryEntriesAsync(query.Trim()).GetAwaiter().GetResult();
        if (entries is null)
            return Error(api.LastError);

        return Json.Serialize(new { 命中数 = entries.Count, 结果 = entries });
    }

    [McpServerTool(Name = "kb_list_memory")]
    [Description("列出全部项目记忆条目（不含正文）。")]
    public string ListMemory()
    {
        var entries = api.GetMemoryEntriesAsync().GetAwaiter().GetResult();
        if (entries is null)
            return Error(api.LastError);

        var items = entries.Select(entry => new
        {
            编号 = entry.Id,
            标题 = entry.Title,
            标签 = entry.Tags,
            更新时间 = entry.UpdatedAt.LocalDateTime
        });

        return Json.Serialize(new { 记忆总数 = entries.Count, 记忆 = items });
    }

    [McpServerTool(Name = "kb_get_memory")]
    [Description("读取一条项目记忆的完整内容。编号来自 kb_search_memory 或 kb_list_memory。")]
    public string GetMemory([Description("记忆条目编号")] string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Error("记忆编号不能为空。");

        var entry = api.GetMemoryEntryAsync(id.Trim()).GetAwaiter().GetResult();
        if (entry is null)
            return Error(api.LastError ?? $"未找到编号为 {id} 的记忆。");

        return Json.Serialize(new
        {
            编号 = entry.Id,
            标题 = entry.Title,
            标签 = entry.Tags,
            更新时间 = entry.UpdatedAt.LocalDateTime,
            正文 = entry.Content
        });
    }

    [McpServerTool(Name = "kb_save_memory")]
    [Description("把值得长期记住的信息（用户偏好、决策、重要结论）保存为项目记忆。id 留空表示新建，填编号表示更新对应条目。")]
    public string SaveMemory(
        [Description("标题（必填），例如“用户代码偏好”")] string title,
        [Description("内容（必填），用简洁的要点描述要记住的信息")] string content,
        [Description("标签，逗号分隔，可留空，例如“偏好,工作流”")] string? tags = null,
        [Description("要更新的记忆编号；新建时留空")] string? id = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error("标题不能为空。");
        if (string.IsNullOrWhiteSpace(content))
            return Error("内容不能为空。");

        var entry = string.IsNullOrWhiteSpace(id)
            ? api.AddMemoryEntryAsync(title.Trim(), content.Trim(), tags).GetAwaiter().GetResult()
            : api.UpdateMemoryEntryAsync(id.Trim(), title.Trim(), content.Trim(), tags).GetAwaiter().GetResult();

        if (entry is null)
            return Error(api.LastError ?? $"未找到编号为 {id} 的记忆，更新失败。");

        return Json.Serialize(new
        {
            已保存 = true,
            编号 = entry.Id,
            标题 = entry.Title,
            新建 = string.IsNullOrWhiteSpace(id)
        });
    }

    private static string Error(string? message) =>
        Json.Serialize(new { 错误 = message ?? "本地知识库 API 不可达。" });
}
