using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;

namespace LocalKnowledgeBase.Core.Tests;

/// <summary>AI 上下文打包逻辑测试（编号、截断、来源映射）。</summary>
public sealed class AiContextBuilderTests
{
    [Fact]
    public void Build_NumbersDocumentsAndMapsSources()
    {
        var documents = new List<Document>
        {
            new() { Id = "d1", Title = "架构说明", Content = "分层架构" },
            new() { Id = "d2", Title = "部署手册", Content = "docker 部署" }
        };

        var context = AiContextBuilder.Build("架构 部署", documents);

        Assert.Equal(2, context.TotalHits);
        Assert.Contains("[1] 架构说明", context.ContextText);
        Assert.Contains("[2] 部署手册", context.ContextText);

        var source = Assert.Single(context.Sources, s => s.Index == 1);
        Assert.Equal("d1", source.DocumentId);
        Assert.Equal(2, context.Sources.Count);
    }

    [Fact]
    public void Build_TruncatesLongContentWithEllipsis()
    {
        var documents = new List<Document>
        {
            new() { Id = "d1", Title = "长文档", Content = new string('字', 2000) }
        };

        var context = AiContextBuilder.Build("长文档", documents, snippetLength: 800);

        Assert.Contains("…", context.ContextText);
        Assert.True(context.ContextText.Length < 2000);
    }

    [Fact]
    public void Build_EmptyDocuments_ReturnsEmptyContext()
    {
        var context = AiContextBuilder.Build("任意查询", new List<Document>());

        Assert.Equal(0, context.TotalHits);
        Assert.Equal(string.Empty, context.ContextText);
        Assert.Empty(context.Sources);
    }

    [Fact]
    public void Build_ClampsTinySnippetLength()
    {
        var documents = new List<Document>
        {
            new() { Id = "d1", Title = "t", Content = new string('字', 500) }
        };

        // snippetLength 传入过小值时被钳制到 50，防止上下文为空
        var context = AiContextBuilder.Build("t", documents, snippetLength: 1);

        Assert.Contains("字", context.ContextText);
    }

    [Fact]
    public void Build_RespectsTotalCharBudget()
    {
        var documents = new List<Document>
        {
            new() { Id = "d1", Title = "一", Content = new string('一', 500) },
            new() { Id = "d2", Title = "二", Content = new string('二', 500) },
            new() { Id = "d3", Title = "三", Content = new string('三', 500) }
        };

        // 预算 900：第一条装满 500+，第二条可能挤进去，第三条必然舍弃
        var context = AiContextBuilder.Build("查询", documents, snippetLength: 500, maxTotalChars: 900);

        Assert.Equal(3, context.TotalHits);
        Assert.True(context.IncludedCount < 3);
        Assert.True(context.Truncated);
        Assert.Single(context.Sources, s => s.Index == 1); // 第一条始终保留
    }

    [Fact]
    public void Build_GenerousBudget_KeepsAllDocuments()
    {
        var documents = new List<Document>
        {
            new() { Id = "d1", Title = "一", Content = "内容一" },
            new() { Id = "d2", Title = "二", Content = "内容二" }
        };

        var context = AiContextBuilder.Build("查询", documents, snippetLength: 100, maxTotalChars: 5000);

        Assert.Equal(2, context.IncludedCount);
        Assert.False(context.Truncated);
        Assert.Contains("[2] 二", context.ContextText);
    }

    [Fact]
    public void Build_FirstDocumentAlwaysIncluded_EvenOverBudget()
    {
        var documents = new List<Document> { new() { Id = "d1", Title = "长文", Content = new string('字', 2000) } };

        var context = AiContextBuilder.Build("长文", documents, snippetLength: 500, maxTotalChars: 200);

        Assert.Equal(1, context.IncludedCount);
        Assert.Contains("长文", context.ContextText);
    }

    [Fact]
    public void SystemPrompt_RequiresCitationAndNoFabrication()
    {
        Assert.Contains("不要编造", AiContextBuilder.SystemPrompt);
        Assert.Contains("[1]", AiContextBuilder.SystemPrompt);
    }
}
