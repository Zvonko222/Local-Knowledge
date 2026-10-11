[CmdletBinding()]
param(
    [ValidateSet("http", "https")]
    [string]$LaunchProfile = "https"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "backend\LocalKnowledgeBase.Api\LocalKnowledgeBase.Api.csproj"

dotnet run --project $project --launch-profile $LaunchProfile
