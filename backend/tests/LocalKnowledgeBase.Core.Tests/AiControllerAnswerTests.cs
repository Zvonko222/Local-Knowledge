using System.Net;
using System.Text;
using LocalKnowledgeBase.Api.Controllers;
using LocalKnowledgeBase.Api.Services;
using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalKnowledgeBase.Core.Tests;

/// <summary>/api/ai/answer 的端到端控制器测试（检索 + 提示词组装 + 模型调用）。</summary>
public sealed class AiControllerAnswerTests
{
    private sealed class StubEmbeddingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"index":0,"embedding":[1,0]}]}""", Encoding.UTF8, "application/json")
            });
    }

    private sealed class StubLlmHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        public string? UserPrompt { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            using var document = System.Text.Json.JsonDocument.Parse(LastBody);
            foreach (var message in document.RootElement.GetProperty("messages").EnumerateArray())
            {
                if (message.GetProperty("role").GetString() == "user")
                    UserPrompt = message.GetProperty("content").GetString();
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"答案依据 [1]"}}]}""", Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task GetAnswer_MemoryOnlyHit_AnswersWithMemorySourceAndMarkedPrompt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lkb-answer-memory-" + Guid.NewGuid().ToString("N"));
        using var knowledgeBase = new KnowledgeBaseService(directory);
        var memory = knowledgeBase.AddMemoryEntry(new MemoryEntry
        {
            Id = "m-answer",
            Title = "Storage decision",
            Content = "Data lives in local files only."
        });
        var vectors = new VectorIndexService(directory);
        vectors.SetChunks(VectorIndexService.MemoryIndexKey(memory.Id), "test-embedding", [
            new VectorChunk { Text = "Data lives in local files only.", Vector = [1f, 0f] }
        ]);
        var embedder = new EmbeddingClient("http://localhost:11434/v1", "test-embedding", handler: new StubEmbeddingHandler());
        var llmHandler = new StubLlmHandler();
        var llm = new LocalLlmClient("http://localhost:11434/v1", "stub-model", handler: llmHandler);
        var controller = new AiController(knowledgeBase, llm, vectors, embedder);

        var action = await controller.GetAnswer(new AiAnswerRequest("where is data stored"), CancellationToken.None);

        var response = Assert.IsType<AiAnswerResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Equal("答案依据 [1]", response.Answer);
        Assert.Equal("stub-model", response.Model);
        var source = Assert.Single(response.Sources);
        Assert.Equal("memory", source.SourceType);
        Assert.Equal("m-answer", source.MemoryEntryId);
        Assert.True(response.SemanticUsed);
        Assert.Contains("[1] [项目记忆] Storage decision", llmHandler.UserPrompt);
        Assert.Contains("Data lives in local files only.", llmHandler.UserPrompt);
    }

    [Fact]
    public async Task GetAnswer_NoHits_ReturnsMessageWithoutCallingLlm()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lkb-answer-empty-" + Guid.NewGuid().ToString("N"));
        using var knowledgeBase = new KnowledgeBaseService(directory);
        var llmHandler = new StubLlmHandler();
        var llm = new LocalLlmClient("http://localhost:11434/v1", "stub-model", handler: llmHandler);
        var controller = new AiController(knowledgeBase, llm);

        var action = await controller.GetAnswer(new AiAnswerRequest("完全没有匹配的内容"), CancellationToken.None);

        var response = Assert.IsType<AiAnswerResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Contains("没有找到", response.Answer);
        Assert.Empty(response.Sources);
        Assert.Null(llmHandler.LastBody);
    }

    [Fact]
    public async Task GetAnswer_NoLlmConfigured_Returns503()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lkb-answer-503-" + Guid.NewGuid().ToString("N"));
        using var knowledgeBase = new KnowledgeBaseService(directory);
        var controller = new AiController(knowledgeBase);

        var action = await controller.GetAnswer(new AiAnswerRequest("任意问题"), CancellationToken.None);

        Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(503, ((ObjectResult)action.Result).StatusCode);
    }
}
