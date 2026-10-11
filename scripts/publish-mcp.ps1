[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "backend\LocalKnowledgeBase.Mcp\LocalKnowledgeBase.Mcp.csproj"
$outDir = Join-Path $root "backend\LocalKnowledgeBase.Mcp\publish"

dotnet publish $project -c $Configuration -o $outDir

# publish 失败（如 exe 被 AI 客户端占用）必须终止，避免误报成功
if ($LASTEXITCODE -ne 0) {
    Write-Host "publish 失败：请先结束正在运行 LocalKnowledgeBase.Mcp.exe 的进程（AI 客户端会话）。" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "MCP 服务器已发布到: $outDir\LocalKnowledgeBase.Mcp.exe"
Write-Host "在 AI 客户端的 MCP 配置中把 command 指向该 exe，"
Write-Host "详细步骤见 docs/tutorial.md。"
