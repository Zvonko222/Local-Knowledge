using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using LocalKnowledgeBase.Desktop.Models;
using LocalKnowledgeBase.Desktop.Services;
using LocalKnowledgeBase.Desktop.Tests.Fakes;
using LocalKnowledgeBase.Desktop.ViewModels;
using Xunit;

namespace LocalKnowledgeBase.Desktop.Tests;

/// <summary>问答（RAG）流程测试：检索 → 上下文 → 本地模型 → 引用来源。</summary>
public sealed class ChatViewModelTests
{
    private static ChatViewModel CreateViewModel(FakeKnowledgeApiClient api, FakeLlmClient llm) =>
        new(api, llm);

    [Fact]
    public async Task Send_WithoutModelConfigured_ShowsGuidanceAndSkipsSearch()
    {
        var api = new FakeKnowledgeApiClient();
        var llm = new FakeLlmClient(); // Model 为空 = 未配置
        var viewModel = CreateViewModel(api, llm);

        viewModel.Question = "知识库里有什么？";
        await viewModel.SendAsync();

        Assert.Equal(0, llm.CompleteCallCount);
        Assert.Single(viewModel.Messages);
        Assert.Contains("尚未配置模型", viewModel.Messages[0].Text);
    }

    [Fact]
    public async Task Send_WithHits_CallsLlmWithSourcesAndAttachesCitations()
    {
        var api = new FakeKnowledgeApiClient();
        api.Documents.Add(new Document { Id = "d1", Title = "架构笔记", Content = "使用分层架构。" });
        var llm = new FakeLlmClient();
        llm.Configure("http://localhost:11434/v1", "test-model");
        var viewModel = CreateViewModel(api, llm);

        viewModel.Question = "架构笔记里说了什么？";
        await viewModel.SendAsync();

        Assert.Equal(1, llm.CompleteCallCount);
        Assert.Equal(2, viewModel.Messages.Count); // 用户问题 + 助手回答
        Assert.Equal("（测试回答）依据资料 [1] 得出结论。", viewModel.Messages[1].Text);

        var source = Assert.Single(viewModel.Messages[1].Sources);
        Assert.Equal("1", source.Index);
        Assert.Equal("架构笔记", source.Title);

        // 提示词里应包含系统约束与检索到的资料
        Assert.Contains("本地知识库助手", llm.LastSystemPrompt);
        Assert.Contains("[1] 架构笔记", llm.LastUserPrompt);
        Assert.Contains("问题：架构笔记里说了什么？", llm.LastUserPrompt);
    }

    [Fact]
    public async Task Send_WithoutHits_ShowsNoResultHintAndSkipsLlm()
    {
        var api = new FakeKnowledgeApiClient(); // 知识库为空
        var llm = new FakeLlmClient();
        llm.Configure("http://localhost:11434/v1", "test-model");
        var viewModel = CreateViewModel(api, llm);

        viewModel.Question = "不存在的主题";
        await viewModel.SendAsync();

        Assert.Equal(0, llm.CompleteCallCount);
        Assert.Contains("没有找到", viewModel.Messages[^1].Text);
    }

    [Fact]
    public async Task Send_WithEmptyQuestion_DoesNothing()
    {
        var api = new FakeKnowledgeApiClient();
        var llm = new FakeLlmClient();
        llm.Configure("http://localhost:11434/v1", "test-model");
        var viewModel = CreateViewModel(api, llm);

        viewModel.Question = "   ";
        await viewModel.SendAsync();

        Assert.Empty(viewModel.Messages);
        Assert.Equal(0, llm.CompleteCallCount);
    }

    [Fact]
    public async Task Send_WhenLlmFails_ShowsReadableError()
    {
        var api = new FakeKnowledgeApiClient();
        api.Documents.Add(new Document { Id = "d1", Title = "架构笔记", Content = "内容" });
        var llm = new FakeLlmClient();
        llm.Configure("http://localhost:11434/v1", "test-model");
        llm.ThrowOnComplete = true;
        var viewModel = CreateViewModel(api, llm);

        viewModel.Question = "架构笔记里写了什么？";
        await viewModel.SendAsync();

        Assert.Contains("调用本地模型失败", viewModel.Messages[^1].Text);
    }
}

/// <summary>OpenAI 兼容客户端的请求/解析逻辑测试（用桩 HttpMessageHandler，不发真实网络请求）。</summary>
public sealed class LlmClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastBody = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(Responder(request));
        }
    }

    private static HttpResponseMessage Json(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    [Fact]
    public void Configure_AppendsV1_WhenUrlHasNoPath()
    {
        var client = new OpenAiCompatibleLlmClient("http://localhost:11434", "qwen2.5:7b");

        Assert.Equal("http://localhost:11434/v1/", client.BaseUrl);
    }

    [Fact]
    public void Configure_KeepsExplicitV1Path()
    {
        var client = new OpenAiCompatibleLlmClient("http://localhost:1234/v1/", "m");

        Assert.Equal("http://localhost:1234/v1/", client.BaseUrl);
    }

    [Fact]
    public void Configure_ThrowsOnInvalidUrl()
    {
        Assert.Throws<ArgumentException>(() => new OpenAiCompatibleLlmClient("不是地址", "m"));
    }

    [Fact]
    public async Task CompleteAsync_PostsChatCompletionsAndParsesAnswer()
    {
        var handler = new StubHandler
        {
            Responder = _ => Json(
                """{"choices":[{"message":{"role":"assistant","content":"本地生成的回答"}}]}""")
        };
        var client = new OpenAiCompatibleLlmClient("http://localhost:11434/v1", "qwen2.5:7b", "", handler);

        var answer = await client.CompleteAsync("system-prompt-marker", "user-question-marker");

        Assert.Equal("本地生成的回答", answer);
        Assert.Equal(new Uri("http://localhost:11434/v1/chat/completions"), handler.LastRequest!.RequestUri);
        Assert.Contains("\"model\":\"qwen2.5:7b\"", handler.LastBody);
        // 中文会被 JSON 序列化转义为 \uXXXX，这里用 ASCII 标记验证提示词已装入请求体
        Assert.Contains("system-prompt-marker", handler.LastBody);
        Assert.Contains("user-question-marker", handler.LastBody);
        // 未配置密钥时不得携带鉴权头
        Assert.False(handler.LastRequest.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task CompleteAsync_WithApiKey_SendsBearerAuthorization()
    {
        var handler = new StubHandler
        {
            Responder = _ => Json("""{"choices":[{"message":{"content":"ok"}}]}""")
        };
        var client = new OpenAiCompatibleLlmClient(
            "https://open.bigmodel.cn/api/paas/v4", "glm-4.7", "test-key-123", handler);

        await client.CompleteAsync("s", "u");

        var auth = Assert.Single(handler.LastRequest!.Headers.GetValues("Authorization"));
        Assert.Equal("Bearer test-key-123", auth);
        // 云端 API 的路径保持原样，不追加 /v1
        Assert.Equal(new Uri("https://open.bigmodel.cn/api/paas/v4/chat/completions"),
            handler.LastRequest.RequestUri);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsFalseWhenOffline()
    {
        var handler = new StubHandler
        {
            Responder = _ => throw new HttpRequestException("refused")
        };
        var client = new OpenAiCompatibleLlmClient("http://localhost:11434/v1", "m", "", handler);

        Assert.False(await client.TestConnectionAsync());
    }
}

public sealed class MemoryAndExportTests
{
    [Fact]
    public async Task MemoryFilter_NarrowsEntries()
    {
        var api = new FakeKnowledgeApiClient();
        await api.AddMemoryEntryAsync("使用偏好", "简洁回复", "偏好");
        await api.AddMemoryEntryAsync("架构决策", "分层架构", "架构");
        var viewModel = new MemoryViewModel(api, new FakeDialogService(), new StatusReporter());
        await viewModel.LoadAsync();

        viewModel.FilterText = "架构";

        Assert.Single(viewModel.Entries);
        Assert.Equal("架构决策", viewModel.Entries[0].Title);
        Assert.Contains("显示 1 / 2", viewModel.StatusText);
    }

    [Fact]
    public void ExportSelected_WritesMarkdownFileWithReportedPath()
    {
        var api = new FakeKnowledgeApiClient();
        var dialogs = new FakeDialogService();
        var viewModel = new DocumentsViewModel(api, dialogs, new StatusReporter(), new Localization());
        viewModel.SelectedDocument = new Document { Id = "d1", Title = "架构:笔记", Content = "正文内容" };
        var exportPath = Path.Combine(Path.GetTempPath(), $"lkb-export-{Guid.NewGuid():N}.md");
        dialogs.NextSavePath = exportPath;

        viewModel.ExportSelected();

        Assert.True(File.Exists(exportPath));
        var content = File.ReadAllText(exportPath);
        Assert.StartsWith("# 架构:笔记", content);
        Assert.Contains("正文内容", content);
        Assert.Contains("已导出", viewModel.StatusText);
        File.Delete(exportPath);
    }

    [Fact]
    public async Task ExportAll_WritesOneFilePerDocumentPlusIndex()
    {
        var api = new FakeKnowledgeApiClient();
        await api.AddDocumentAsync("架构笔记", "a.md", [1]);
        await api.AddDocumentAsync("会议记录", "b.md", [2]);
        var dialogs = new FakeDialogService();
        var viewModel = new DocumentsViewModel(api, dialogs, new StatusReporter(), new Localization());
        var exportDir = Path.Combine(Path.GetTempPath(), $"lkb-export-all-{Guid.NewGuid():N}");
        dialogs.NextFolder = exportDir;

        await viewModel.ExportAllAsync();

        Assert.True(File.Exists(Path.Combine(exportDir, "架构笔记.md")));
        Assert.True(File.Exists(Path.Combine(exportDir, "会议记录.md")));
        var index = File.ReadAllText(Path.Combine(exportDir, "_索引.md"));
        Assert.Contains("文档数量：2", index);
        Assert.Contains("架构笔记.md", index);
        Assert.Contains("已导出 2 篇文档", viewModel.StatusText);
        Directory.Delete(exportDir, recursive: true);
    }

    [Fact]
    public async Task BackupData_CreatesZipOfDataDirectory()
    {
        var api = new FakeKnowledgeApiClient();
        var dataDir = Path.Combine(Path.GetTempPath(), "lkb-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);
        await File.WriteAllTextAsync(Path.Combine(dataDir, "documents.json"), "{}");
        api.NextDataDirectory = new GetDataDirectoryResponse { Path = dataDir, Configured = true };

        var dialogs = new FakeDialogService();
        var viewModel = new SettingsViewModel(api, new FakeLlmClient(), dialogs, new StatusReporter(), new Localization());
        await viewModel.RefreshAsync();

        var zipPath = Path.Combine(Path.GetTempPath(), $"lkb-backup-{Guid.NewGuid():N}.zip");
        dialogs.NextSavePath = zipPath;

        await viewModel.BackupDataAsync();

        Assert.True(File.Exists(zipPath));
        Assert.Contains("已备份到", viewModel.DirectoryStatusText);
        Directory.Delete(dataDir, recursive: true);
        File.Delete(zipPath);
    }

    [Fact]
    public async Task ApplyProfileAsync_SwitchesPresetViaApi()
    {
        var api = new FakeKnowledgeApiClient();
        var viewModel = new SettingsViewModel(api, new FakeLlmClient(), new FakeDialogService(), new StatusReporter(), new Localization());

        viewModel.ProfileLabel = SettingsViewModel.ProfileLightLabel;
        await viewModel.ApplyProfileAsync();

        Assert.Equal("light", api.CurrentProfile);
        Assert.Contains("已切换", viewModel.ProfileStatusText);
        Assert.Contains("0.4", viewModel.ProfileStatusText);
    }

    [Fact]
    public void SelectingProvider_AutoFillsDeepSeekDefaults()
    {
        var viewModel = new SettingsViewModel(new FakeKnowledgeApiClient(), new FakeLlmClient(),
            new FakeDialogService(), new StatusReporter(), new Localization());

        viewModel.SelectedProvider = "DeepSeek";

        Assert.Equal("https://api.deepseek.com/v1", viewModel.LlmBaseUrl);
        Assert.Equal("deepseek-chat", viewModel.LlmModel);
    }

    [Fact]
    public async Task ExportAll_WithEmptyKnowledgeBase_DoesNotAskForFolder()
    {
        var api = new FakeKnowledgeApiClient();
        var dialogs = new FakeDialogService();
        var viewModel = new DocumentsViewModel(api, dialogs, new StatusReporter(), new Localization());

        await viewModel.ExportAllAsync();

        Assert.Contains("知识库为空", viewModel.StatusText);
        Assert.Null(dialogs.NextFolder);
    }
}
