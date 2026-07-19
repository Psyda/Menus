# Builds a single-file DoneYet.exe into DoneYet\dist\win-x64\
# Usage:  .\publish.ps1            (self-contained, ~70 MB, needs nothing installed)
#         .\publish.ps1 -Small     (framework-dependent, ~1 MB, needs .NET 8 Desktop Runtime)
param([switch]$Small)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$selfContained = if ($Small) { "false" } else { "true" }

dotnet publish DoneYet.csproj `
    -c Release `
    -r win-x64 `
    --self-contained $selfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o dist/win-x64

Write-Host ""
Write-Host "Done -> $PSScriptRoot\dist\win-x64\DoneYet.exe"
