#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
exec dotnet run --project Glyphore.csproj -- "$@"
