[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$legacyPaths = @(
    (Join-Path $root "front-end"),
    (Join-Path $root ".legacy-front-end"),
    (Join-Path $root ".vs")
)

foreach ($path in $legacyPaths)
{
    if (-not (Test-Path -LiteralPath $path))
    {
        continue
    }

    try
    {
        Remove-Item -LiteralPath $path -Recurse -Force
        Write-Host "Removed $path"
    }
    catch
    {
        Write-Warning "Could not remove $path. Close Visual Studio, Explorer, or shell sessions using it and retry."
    }
}
