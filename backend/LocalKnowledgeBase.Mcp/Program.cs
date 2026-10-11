using LocalKnowledgeBase.Mcp.Api;
using LocalKnowledgeBase.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

// 本地知识库的 MCP (Model Context Protocol) 服务器，基于 stdio 传输。
// 由 AI 客户端（ZCode、Claude Desktop、Cherry Studio 等）作为子进程启动，
// 通过 stdin/stdout 交换 JSON-RPC 报文。
//
// 重要：stdio 模式下 stdout 只能输出 MCP 协议报文。
// 日志一律走 stderr（见下方日志配置），不要往 Console.WriteLine 打印。

var apiBaseUrl = Environment.GetEnvironmentVariable("LOCAL_KNOWLEDGE_BASE_API_URL");
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    apiBaseUrl = "http://localhost:5062"; // 与 scripts/run-api.ps1 默认端口一致

var services = new ServiceCollection();

// 日志输出到 stderr：stdout 是 MCP 协议专用通道
services.AddLogging(logging => logging
    .SetMinimumLevel(LogLevel.Information)
    .AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace));

services.AddSingleton(_ => new KnowledgeBaseApiClient(apiBaseUrl));
services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(); // 扫描本程序集中所有 [McpServerToolType]

var provider = services.BuildServiceProvider();
var server = provider.GetRequiredService<McpServer>();
await server.RunAsync();
