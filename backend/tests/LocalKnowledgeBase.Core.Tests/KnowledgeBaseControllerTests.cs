using System.Net;
using System.Text;
using LocalKnowledgeBase.Api.Controllers;
using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalKnowledgeBase.Core.Tests;

/// <summary>知识库 HTTP 控制器的记忆搜索语义与条数限制测试。</summary>
public sealed class KnowledgeBaseControllerTests
{
    private sealed class StubEmbeddingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"index":0,"embedding":[1,0]}]}""", Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task SearchMemory_WithQueryVector_FindsSemanticMatchWithoutKeywordOverlap()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lkb-memory-endpoint-" + Guid.NewGuid().ToString("N"));
        using var knowledgeBase = new KnowledgeBaseService(directory);
        var memory = knowledgeBase.AddMemoryEntry(new MemoryEntry { Id = "m1", Title = "存储决策", Content = "本地优先持久化" });
        var vectors = new VectorIndexService(directory);
        vectors.SetChunks(VectorIndexService.MemoryIndexKey(memory.Id), "test-embedding", [
            new VectorChunk { Text = "本地优先持久化", Vector = [1f, 0f] }
        ]);
        var embedder = new EmbeddingClient("http://localhost:11434/v1", "test-embedding", handler: new StubEmbeddingHandler());
        var controller = new KnowledgeBaseController(knowledgeBase, vectors, embedder);

        var action = await controller.SearchMemory("distributed storage", null);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var entries = Assert.IsType<List<MemoryEntry>>(result.Value);
        var entry = Assert.Single(entries);
        Assert.Equal("m1", entry.Id);
    }

    [Fact]
    public async Task SearchMemory_RespectsMaxResults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lkb-memory-max-" + Guid.NewGuid().ToString("N"));
        using var knowledgeBase = new KnowledgeBaseService(directory);
        knowledgeBase.AddMemoryEntry(new MemoryEntry { Id = "m1", Title = "偏好", Content = "简洁 回答" });
        knowledgeBase.AddMemoryEntry(new MemoryEntry { Id = "m2", Title = "约定", Content = "简洁 注释" });
        var controller = new KnowledgeBaseController(knowledgeBase);

        var action = await controller.SearchMemory("简洁", 1);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var entries = Assert.IsType<List<MemoryEntry>>(result.Value);
        Assert.Single(entries);
    }
}
