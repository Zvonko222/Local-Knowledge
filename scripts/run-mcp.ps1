[CmdletBinding()]
param(
    # MCP 服务器要连接的本地 API 地址，需与 API 实际监听的端口一致
    [string]$ApiBaseUrl = "http://localhost:5062"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "backend\LocalKnowledgeBase.Mcp\LocalKnowledgeBase.Mcp.csproj"

# 先启动 API（另开一个终端运行 scripts\run-api.ps1），再运行本脚本调试 MCP 服务器
$env:LOCAL_KNOWLEDGE_BASE_API_URL = $ApiBaseUrl

dotnet run --project $project
