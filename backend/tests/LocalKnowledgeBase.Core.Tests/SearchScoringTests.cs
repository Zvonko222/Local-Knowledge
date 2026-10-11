using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;

namespace LocalKnowledgeBase.Core.Tests;

/// <summary>
/// 检索打分逻辑测试：分词、多关键词命中、按相关度排序。
/// </summary>
public sealed class SearchScoringTests
{
    [Fact]
    public void TokenizeQuery_SplitsByWhitespaceAndPunctuation()
    {
        var terms = KnowledgeBaseService.TokenizeQuery("项目采用了什么架构？数据保存在哪里？");

        Assert.Contains("项目采用了什么架构", terms);
        Assert.Contains("数据保存在哪里", terms);
        Assert.Equal(2, terms.Count);
    }

    [Fact]
    public void TokenizeQuery_DeduplicatesIgnoringCase()
    {
        var terms = KnowledgeBaseService.TokenizeQuery("WPF wpf 架构");

        Assert.Equal(2, terms.Count);
    }

    [Fact]
    public void ScoreText_TitleWeightedHigherThanContent()
    {
        var terms = KnowledgeBaseService.TokenizeQuery("架构");

        var titleMatch = KnowledgeBaseService.ScoreText("架构笔记", null, "无关内容", terms);
        var contentMatch = KnowledgeBaseService.ScoreText("随便的标题", null, "提到了架构这个词", terms);

        Assert.True(titleMatch > contentMatch);
        Assert.Equal(8, titleMatch);
        Assert.Equal(2, contentMatch); // 正文按出现次数 ×2（单次 = 2）
    }

    [Fact]
    public void ScoreText_ContentFrequencyAddsScore_WithCap()
    {
        var terms = KnowledgeBaseService.TokenizeQuery("docker");

        var once = KnowledgeBaseService.ScoreText("t", null, "用到 docker 一次", terms);
        var many = KnowledgeBaseService.ScoreText("t", null, "docker docker docker docker docker docker docker", terms);

        Assert.Equal(2, once);
        Assert.Equal(10, many); // 出现次数上限 5 次（×2）
        Assert.True(many > once);
    }

    [Fact]
    public void ScoreText_AllTermsCovered_GetsBonus()
    {
        var terms = KnowledgeBaseService.TokenizeQuery("架构 部署");

        var both = KnowledgeBaseService.ScoreText("t", null, "架构与部署都提到", terms);
        var half = KnowledgeBaseService.ScoreText("t", null, "只提到架构", terms);

        Assert.Equal(2, half);               // 单词单次（正文 ×2）
        Assert.True(both > half + 8);        // 两词命中 + 覆盖加成 8
    }

    [Fact]
    public void BestSnippet_CentersOnFirstTermHit()
    {
        var filler = new string('占', 600);
        var text = filler + "这里出现关键词docker的位置在中间。" + new string('占', 600);
        var terms = KnowledgeBaseService.TokenizeQuery("docker");

        var snippet = KnowledgeBaseService.BestSnippet(text, terms, 200);

        Assert.Contains("docker", snippet);
        // 摘要应从命中位置附近开始，而不是文档开头
        Assert.True(snippet.IndexOf("docker", StringComparison.Ordinal) < 200);
        Assert.StartsWith("…", snippet);
    }

    [Fact]
    public void BestSnippet_NoHit_FallsBackToHead()
    {
        var text = new string('占', 600);
        var snippet = KnowledgeBaseService.BestSnippet(text, ["docker"], 200);

        Assert.DoesNotContain("…", snippet[..1]);
        Assert.StartsWith("占", snippet);
        Assert.EndsWith("…", snippet);
    }

    [Fact]
    public void SearchDocuments_PastedSentenceWithVerbatimPhrases_FindsDocument()
    {
        // 分词按标点切分：整段粘贴的文本里，与原文逐字一致的短语都能作为关键词命中
        var service = new KnowledgeBaseService(CreateTempDir());
        service.AddDocument(new Document
        {
            Title = "架构说明",
            Content = "本项目采用 WPF 桌面端加 ASP.NET Core API 的分层架构，所有数据保存在本机。"
        });

        var results = service.SearchDocuments("分层架构，数据保存在本机？");

        Assert.Single(results);
        Assert.Equal("架构说明", results[0].Title);
    }

    [Fact]
    public void SearchDocuments_OrdersByRelevanceThenUpdatesAt()
    {
        var service = new KnowledgeBaseService(CreateTempDir());
        service.AddDocument(new Document { Title = "架构总览", Content = "内容" });
        service.AddDocument(new Document { Title = "随笔", Content = "顺便聊一下架构" });
        service.AddDocument(new Document { Title = "无关文档", Content = "没有任何关键词" });

        var results = service.SearchDocuments("架构");

        Assert.Equal(2, results.Count);
        Assert.Equal("架构总览", results[0].Title); // 标题命中排在正文命中前面
        Assert.Equal("随笔", results[1].Title);
    }

    [Fact]
    public void SearchDocuments_MultiTermScoresSum()
    {
        var service = new KnowledgeBaseService(CreateTempDir());
        service.AddDocument(new Document { Title = "部署手册", Content = "包含 docker 部署步骤" });
        service.AddDocument(new Document { Title = "Docker 入门", Content = "容器基础" });

        var results = service.SearchDocuments("docker 部署");

        Assert.Equal(2, results.Count);
        Assert.Equal("部署手册", results[0].Title); // 两个词都命中，得分更高
    }

    [Fact]
    public void SearchMemoryEntries_UsesSameScoring()
    {
        var service = new KnowledgeBaseService(CreateTempDir());
        service.AddMemoryEntry(new MemoryEntry { Title = "用户偏好", Content = "回复保持简洁" });
        service.AddMemoryEntry(new MemoryEntry { Title = "会议纪要", Content = "讨论了偏好设置" });

        var results = service.SearchMemoryEntries("用户 偏好");

        // OR 语义：命中任一关键词即返回；标题命中的“用户偏好”得分最高排在最前
        Assert.Equal(2, results.Count);
        Assert.Equal("用户偏好", results[0].Title);
    }

    [Fact]
    public void SearchDocuments_EmptyQuery_ReturnsEmpty()
    {
        var service = new KnowledgeBaseService(CreateTempDir());
        service.AddDocument(new Document { Title = "架构说明", Content = "内容" });

        Assert.Empty(service.SearchDocuments("   "));
        Assert.Empty(service.SearchDocuments("，。？"));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lkb-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
