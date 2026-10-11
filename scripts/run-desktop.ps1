[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "frontend\desktop\LocalKnowledgeBase.Desktop\LocalKnowledgeBase.Desktop.csproj"

dotnet run --project $project -c $Configuration
