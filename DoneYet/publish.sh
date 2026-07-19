#!/usr/bin/env bash
# Cross-builds the Windows single-file DoneYet.exe from Linux/macOS.
# Usage: ./publish.sh          (self-contained, ~70 MB)
#        ./publish.sh --small  (framework-dependent, ~1 MB, needs .NET 8 Desktop Runtime on the PC)
set -euo pipefail
cd "$(dirname "$0")"

SELF_CONTAINED=true
[[ "${1:-}" == "--small" ]] && SELF_CONTAINED=false

dotnet publish DoneYet.csproj \
    -c Release \
    -r win-x64 \
    --self-contained "$SELF_CONTAINED" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o dist/win-x64

echo
echo "Done -> $(pwd)/dist/win-x64/DoneYet.exe"
