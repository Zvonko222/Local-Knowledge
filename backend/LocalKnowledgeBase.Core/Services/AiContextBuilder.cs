namespace LocalKnowledgeBase.Core.Services;

/// <summary>回答引用的一条来源。</summary>
public class AiSource
{
    /// <summary>上下文中的序号，从 1 开始。</summary>
    public int Index { get; init; }

    public string Title { get; init; } = string.Empty;

    /// <summary>对应的知识库文档 Id，可用于 kb_get_document / GET /documents/{id} 取全文。</summary>
    public string DocumentId { get; init; } = string.Empty;
}

/// <summary>为 AI 准备的检索上下文：可直接注入大模型提示词的文本 + 来源列表。</summary>
public class AiContext
{
    public string Query { get; init; } = string.Empty;

    /// <summary>检索命中的总条数（含未进入上下文的）。</summary>
    public int TotalHits { get; init; }

    /// <summary>实际进入上下文的条数（受 <paramref name="maxTotalChars"/> 预算限制）。</summary>
    public int IncludedCount { get; init; }

    /// <summary>是否有命中条目因超出预算被舍弃。</summary>
    public bool Truncated { get; init; }

    /// <summary>打包好的上下文文本，格式：“[1] 标题\n相关内容摘要”，条目之间空行分隔。</summary>
    public string ContextText { get; init; } = string.Empty;

    public IReadOnlyList<AiSource> Sources { get; init; } = Array.Empty<AiSource>();
}

/// <summary>
/// 把检索结果打包成“喂给大模型”的上下文：统一编号、查询相关摘要、引用格式。
/// 摘要窗口以查询关键词的命中位置为中心（而非从头截断），与检索打分共用同一套分词。
/// </summary>
public static class AiContextBuilder
{
    /// <summary>问答类提示词的系统约束：只依据资料回答并标注引用，不编造。</summary>
    public const string SystemPrompt =
        "你是本地知识库助手。请只依据用户提供的资料回答问题；" +
        "资料不足以回答时明确说明，不要编造。用中文回答，" +
        "并在引用某条资料的位置标注其序号，如 [1]、[2]。";

    /// <summary>每条资料进入上下文的最大长度（字符），控制提示词规模。</summary>
    public const int DefaultSnippetLength = 800;

    /// <summary>
    /// 打包检索结果。documents 需已按相关度排序（SearchDocuments 的返回顺序）。
    /// maxTotalChars 为上下文总字符预算：按相关度从高到低装入，装不下即停
    /// （第一条始终保留），用于把单次注入的 token 消耗控制在预算内。
    /// </summary>
    public static AiContext Build(string query, IReadOnlyList<Document> documents, int snippetLength = DefaultSnippetLength,
        int maxTotalChars = int.MaxValue)
    {
        if (snippetLength < 50)
            snippetLength = 50;
        if (maxTotalChars < 200)
            maxTotalChars = 200;

        var terms = KnowledgeBaseService.TokenizeQuery(query);
        var builder = new System.Text.StringBuilder();
        var sources = new List<AiSource>();
        var used = 0;
        var truncated = false;

        for (var i = 0; i < documents.Count; i++)
        {
            var document = documents[i];
            var snippet = KnowledgeBaseService.BestSnippet(document.Content, terms, snippetLength);
            var blockLength = document.Title.Length + snippet.Length + 16; // 编号、换行等开销估算

            // 第一条始终保留；后续条目放不下即停（剩余的舍弃并标记截断）
            if (sources.Count > 0 && used + blockLength > maxTotalChars)
            {
                truncated = true;
                break;
            }

            if (builder.Length > 0)
                builder.AppendLine().AppendLine();
            builder.Append($"[{i + 1}] {document.Title}").AppendLine();
            builder.Append(snippet);
            used += blockLength;

            sources.Add(new AiSource
            {
                Index = i + 1,
                Title = document.Title,
                DocumentId = document.Id
            });
        }

        return new AiContext
        {
            Query = query,
            TotalHits = documents.Count,
            IncludedCount = sources.Count,
            Truncated = truncated,
            ContextText = builder.ToString(),
            Sources = sources
        };
    }
}
