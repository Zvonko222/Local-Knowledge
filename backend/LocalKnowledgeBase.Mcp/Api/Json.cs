using System.Text.Encodings.Web;
using System.Text.Json;

namespace LocalKnowledgeBase.Mcp.Api;

/// <summary>MCP 工具返回内容用的 JSON 序列化配置：缩进 + 中文不转义，便于模型与人工阅读。</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>把长文本截断成摘要，避免搜索结果占满模型上下文。</summary>
    public static string Snippet(string text, int maxLength = 400) =>
        string.IsNullOrEmpty(text) || text.Length <= maxLength
            ? text
            : text[..maxLength] + "…";
}
