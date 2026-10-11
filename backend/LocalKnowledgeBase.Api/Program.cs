using LocalKnowledgeBase.Api.Services;
using LocalKnowledgeBase.Core.Services;
using LocalKnowledgeBase.Core;

var builder = WebApplication.CreateBuilder(args);

// Get or create the data directory
var dataDir = KnowledgeBaseService.GetUserDataDirectory();
if (string.IsNullOrEmpty(dataDir))
{
    // Keep user data outside the installation/repository directory by default.
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (string.IsNullOrWhiteSpace(localAppData))
        localAppData = Path.GetTempPath();

    dataDir = Path.Combine(localAppData, "LocalKnowledgeBase", "data");
    Directory.CreateDirectory(dataDir);
}

var kbService = new KnowledgeBaseService(dataDir);
builder.Services.AddSingleton(kbService);
builder.Services.AddSingleton(new VectorIndexService(dataDir));

// 本地大模型（/api/ai/answer 用）：通过 LOCAL_KNOWLEDGE_BASE_LLM_URL / _MODEL 环境变量配置，
// 云端 API 另需 LOCAL_KNOWLEDGE_BASE_LLM_KEY；未配置时 /api/ai/answer 返回 503
var llmClient = LocalLlmClient.FromEnvironment();
if (llmClient is not null)
    builder.Services.AddSingleton(llmClient);

// Embedding 模型（语义检索用）：LOCAL_KNOWLEDGE_BASE_EMBED_MODEL 配置模型名，
// 地址与密钥复用 LLM 的两个环境变量；未配置时检索退化为纯关键词模式
var embedModel = Environment.GetEnvironmentVariable("LOCAL_KNOWLEDGE_BASE_EMBED_MODEL");
if (!string.IsNullOrWhiteSpace(embedModel))
{
    var embedUrl = Environment.GetEnvironmentVariable("LOCAL_KNOWLEDGE_BASE_LLM_URL") ?? "http://localhost:11434/v1";
    var embedKey = Environment.GetEnvironmentVariable("LOCAL_KNOWLEDGE_BASE_LLM_KEY") ?? string.Empty;
    builder.Services.AddSingleton(new EmbeddingClient(embedUrl, embedModel, embedKey));
}

// 检索预算档位（light/standard/deep）：所有检索入口在请求未显式指定参数时套用；
// 可经 /api/ai/profile 读写，AI 亦可经 MCP 工具运行时切换
builder.Services.AddSingleton<RetrievalProfileService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
