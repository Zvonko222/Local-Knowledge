[CmdletBinding()]
param(
    # MCP 服务器与桌面端默认连接 http://localhost:5062（https profile 同时监听 7047）
    [ValidateSet("http", "https")]
    [string]$LaunchProfile = "https",
    # 语义检索用的 embedding 模型名（如 embedding-3 / nomic-embed-text）；留空 = 纯关键词检索
    [string]$EmbedModel = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "backend\LocalKnowledgeBase.Api\LocalKnowledgeBase.Api.csproj"

# 后台启动本地 API（最小化窗口）。AI 的 MCP 工具与桌面端都依赖它，建议保持运行。
# 停止：关闭弹出的那个最小化窗口，或在任务管理器结束 LocalKnowledgeBase.Api。
if ($EmbedModel -ne "") {
    $env:LOCAL_KNOWLEDGE_BASE_EMBED_MODEL = $EmbedModel
}

Start-Process -WindowStyle Minimized powershell -ArgumentList (
    "-NoProfile -ExecutionPolicy Bypass -Command `"dotnet run --project '$project' --launch-profile $LaunchProfile`"")
