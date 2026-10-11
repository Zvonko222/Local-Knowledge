using System.Net.Http.Headers;
using System.Text;
using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;

namespace LocalKnowledgeBase.Core.Tests;

public sealed class VectorIndexServiceTests
{
    [Fact]
    public void SetGetRemove_BasicLifecycle()
    {
        var service = new VectorIndexService(CreateTempDir());

        service.Set("d1", "test-model", [1f, 0f]);
        service.Set("d2", "test-model", [0f, 1f]);

        Assert.Equal(2, service.Count);
        Assert.Equal("test-model", service.StoredModel);
        Assert.Equal("test-model", service.Get("d1")!.Model);

        service.Remove("d1");
        Assert.Null(service.Get("d1"));
        Assert.Equal(1, service.Count);
    }

    [Fact]
    public void Index_PersistsAcrossInstances()
    {
        var dir = CreateTempDir();
        new VectorIndexService(dir).Set("d1", "m1", [1f, 2f, 3f]);

        var reloaded = new VectorIndexService(dir);
        var record = reloaded.Get("d1");

        Assert.NotNull(record);
        Assert.Equal("m1", record.Model);
        Assert.Equal(new[] { 1f, 2f, 3f }, record.Vector);
    }

    [Fact]
    public void RemoveExcept_CleansOrphanVectors()
    {
        var service = new VectorIndexService(CreateTempDir());
        service.Set("keep", "m", [1f]);
        service.Set("orphan", "m", [2f]);

        service.RemoveExcept(["keep"]);

        Assert.NotNull(service.Get("keep"));
        Assert.Null(service.Get("orphan"));
    }

    [Theory]
    [InlineData(new[] { 1f, 0f }, new[] { 1f, 0f }, 1.0)]   // 同向
    [InlineData(new[] { 1f, 0f }, new[] { 0f, 1f }, 0.0)]   // 正交
    [InlineData(new[] { 1f, 0f }, new[] { -1f, 0f }, -1.0)] // 相反
    public void CosineSimilarity_KnownValues(float[] a, float[] b, double expected)
    {
        Assert.Equal(expected, VectorIndexService.CosineSimilarity(a, b), precision: 6);
    }

    [Fact]
    public void FindMostSimilar_RanksByCosine_ExcludesSelfAndMismatched()
    {
        var service = new VectorIndexService(CreateTempDir());
        service.Set("A", "m", [1f, 0f]);
        service.Set("B", "m", [0.9f, 0.1f]);  // 与 A 高度相似
        service.Set("C", "m", [0f, 1f]);      // 正交
        service.Set("D", "m", [1f, 0f, 0f]);  // 维度不一致（换模型遗留）应被排除

        var related = service.FindMostSimilar("A", 5);

        Assert.Single(related);              // D 被排除；C 相似度 0 被过滤
        Assert.Equal("B", related[0].DocumentId);
        Assert.True(related[0].Similarity > 0.9);
    }

    [Fact]
    public void FindMostSimilar_NoSourceVector_ReturnsEmpty()
    {
        var service = new VectorIndexService(CreateTempDir());
        service.Set("B", "m", [1f, 0f]);

        Assert.Empty(service.FindMostSimilar("A", 5));
    }

    [Fact]
    public void CosineSimilarity_MismatchedLength_ReturnsZero()
    {
        Assert.Equal(0, VectorIndexService.CosineSimilarity([1f, 0f], [1f, 0f, 0f]));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lkb-vector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

/// <summary>混合检索（关键词 + 语义向量）测试。</summary>
public sealed class HybridSearchTests
{
    private static KnowledgeBaseService CreateService()
    {
        var service = new KnowledgeBaseService(Path.Combine(Path.GetTempPath(), "lkb-hybrid-" + Guid.NewGuid().ToString("N")));
        // 文档 A：与 docker 强相关；文档 B：与"容器编排"语义相近但无关键词重叠；文档 C：无关
        service.AddDocument(new Document { Id = "A", Title = "部署手册", Content = "docker 部署步骤" });
        service.AddDocument(new Document { Id = "B", Title = "容器化方案", Content = "微服务容器化改造" });
        service.AddDocument(new Document { Id = "C", Title = "团队规范", Content = "代码评审流程" });
        return service;
    }

    [Fact]
    public void Search_WithQueryVector_FindsSemanticMatchWithoutKeywordOverlap()
    {
        var service = CreateService();
        var index = new VectorIndexService(service.DataDirectory);
        // B 的向量与查询向量接近（模拟"容器编排"≈"容器化"）
        index.Set("B", "m", [0.9f, 0.1f]);

        // 查询"容器编排"不含任何文档词面，纯关键词检索为空；带向量后命中 B
        var withVector = service.SearchDocuments("容器编排", 20, index, [1f, 0f]);
        var withoutVector = service.SearchDocuments("容器编排", 20);

        Assert.Empty(withoutVector);
        Assert.Single(withVector);
        Assert.Equal("B", withVector[0].Id);
    }

    [Fact]
    public void Search_KeywordAndSemanticCombine_InHybridScore()
    {
        var service = CreateService();
        var index = new VectorIndexService(service.DataDirectory);
        index.Set("A", "m", [1f, 0f]);   // docker 文档：语义强相关
        index.Set("B", "m", [0.7f, 0.7f]); // 容器化文档：语义次之

        // 查询词 "docker"（A 标题无……A 标题"部署手册"不含 docker，A 正文命中 +1；B 正文无 docker）
        // 语义：A 余弦 1.0，B 约 0.7
        var results = service.SearchDocuments("docker", 20, index, [1f, 0f]);

        Assert.Equal(2, results.Count);
        Assert.Equal("A", results[0].Id); // 关键词分 + 最高语义分
        Assert.Equal("B", results[1].Id); // 仅语义分
    }

    [Fact]
    public void Search_ModelMismatch_VectorIgnored()
    {
        var service = CreateService();
        var index = new VectorIndexService(service.DataDirectory);
        index.Set("B", "old-model", [1f, 0f]); // 索引用旧模型生成

        // 调用方在模型不一致时不应传 queryVector（控制器负责校验）；这里验证纯关键词路径不受影响
        var results = service.SearchDocuments("docker", 20, index, null);

        Assert.Single(results);
        Assert.Equal("A", results[0].Id);
    }
}

/// <summary>EmbeddingClient 的请求/解析测试（桩 HttpMessageHandler，不发真实网络请求）。</summary>
public sealed class EmbeddingClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastBody = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(Responder(request));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task EmbedBatchAsync_ParsesVectorsAndSendsModel()
    {
        var handler = new StubHandler
        {
            Responder = _ => Json("""{"data":[{"index":0,"embedding":[0.1,0.9]},{"index":1,"embedding":[0.5,0.5]}]}""")
        };
        var client = new EmbeddingClient("http://localhost:11434/v1", "nomic-embed-text", "secret", handler);

        var vectors = await client.EmbedBatchAsync(["first", "second"]);

        Assert.NotNull(vectors);
        Assert.Equal(2, vectors.Count);
        Assert.Equal(new[] { 0.1f, 0.9f }, vectors[0]);
        Assert.Equal("Bearer secret",
            Assert.Single(handler.LastRequest!.Headers.GetValues("Authorization")));
        Assert.Contains("\"model\":\"nomic-embed-text\"", handler.LastBody);
    }

    [Fact]
    public async Task EmbedAsync_OnHttpFailure_ReturnsNullWithLastError()
    {
        var handler = new StubHandler
        {
            Responder = _ => throw new HttpRequestException("refused")
        };
        var client = new EmbeddingClient("http://localhost:11434/v1", "m", "", handler);

        var vector = await client.EmbedAsync("text");

        Assert.Null(vector);
        Assert.NotNull(client.LastError);
    }
}

/// <summary>检索预算档位持久化测试。</summary>
public sealed class RetrievalProfileServiceTests
{
    [Fact]
    public void Set_PersistsAcrossInstances()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lkb-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var service = new RetrievalProfileService(dir);
        service.Set("light");
        Assert.Equal("light", service.Get().Preset);

        var reloaded = new RetrievalProfileService(dir);
        Assert.Equal("light", reloaded.Get().Preset);
        Assert.Equal(3, reloaded.Get().MaxResults);
        Assert.Equal(0.4, reloaded.Get().TokenWeight);
    }

    [Fact]
    public void Resolve_UnknownPreset_FallsBackToStandard()
    {
        Assert.Equal("standard", RetrievalProfileService.Resolve("乱写的").Preset);
        Assert.Equal("light", RetrievalProfileService.Resolve("LIGHT").Preset);
        Assert.Equal("deep", RetrievalProfileService.Resolve(" deep ").Preset);
    }
}
