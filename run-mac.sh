#!/usr/bin/env bash
# Debug build & run SecretBase.App.Mac (Avalonia). Safe Exit is process end only.
# Auto-start is not available while using `dotnet run`.
set -euo pipefail
root="$(cd "$(dirname "$0")" && pwd)"
cd "$root"
dotnet run --project src/SecretBase.App.Mac/SecretBase.App.Mac.csproj -c Debug
